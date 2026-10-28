using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Repositories
{
    public class AuthService : IAuthService
    {
        private readonly DataContext _context;
        private readonly IJwtService _jwtService;
        private readonly ICurrentUserService _currentUserService;
        private readonly INotificationService _notifications;
        private readonly IConfiguration _configuration;

        public AuthService(
            DataContext context,
            IJwtService jwtService,
            ICurrentUserService currentUserService,
            INotificationService notifications,
            IConfiguration configuration)
        {
            _configuration = configuration;
            _notifications = notifications;
            _context = context;
            _jwtService = jwtService;
            _currentUserService = currentUserService;
        }

        /// <summary>
        /// الدخول مع الحماية من التخمين (LoginRules): 5 محاولات خاطئة متتالية تقفل الحساب 15 دقيقة (ولو أُدخلت الكلمة
        /// الصحيحة خلالها) ويُشعَر صاحبه؛ الدخول الصحيح يصفّر العدّاد. البريد غير الموجود والكلمة الخاطئة رسالة واحدة.
        /// </summary>
        public async Task<LoginOutcome> LoginAsync(LoginRequest request)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Department)
                .Include(u => u.Branch)
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null || !user.IsActive) return LoginOutcome.Invalid;

            var now = DateTime.UtcNow;
            if (user.LockoutUntil > now) return new LoginOutcome(null, user.LockoutUntil);

            // التحقق من كلمة المرور باستخدام BCrypt
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                user.FailedLoginCount++;
                if (user.FailedLoginCount < LoginRules.MaxFailedAttempts)
                {
                    await _context.SaveChangesAsync();
                    return LoginOutcome.Invalid;
                }
                user.FailedLoginCount = 0;
                user.LockoutUntil = now.AddMinutes(LoginRules.LockoutMinutes);
                await _context.SaveChangesAsync();
                await _notifications.AddAsync(new Notification
                {
                    UserId = user.Id,
                    Title = "محاولات دخول خاطئة على حسابك",
                    Message = $"جرت {LoginRules.MaxFailedAttempts} محاولات دخول خاطئة على حسابك فأُوقف الدخول إليه {LoginRules.LockoutMinutes} دقيقة. إن لم تكن أنت فأبلغ المسؤول عن النظام.",
                    Type = NotificationType.LoginLocked,
                    CreatedAt = now
                });
                return new LoginOutcome(null, user.LockoutUntil);
            }

            user.FailedLoginCount = 0;
            user.LockoutUntil = null;

            var started = DateTime.UtcNow;
            var expiresAt = _jwtService.GetExpirationDate();
            return new LoginOutcome(await IssueAsync(user, started, expiresAt), null);
        }

        private int MaxSessionHours => _configuration.GetValue("JwtSettings:MaxSessionHours", SessionRules.DefaultMaxSessionHours);
        private int DurationMinutes => _configuration.GetValue("JwtSettings:DurationInMinutes", 60);

        /// <summary>يُصدر توكناً ويحفظه (لإبطاله لاحقاً) ويبني ردّ الدخول: يُستعمل عند الدخول وعند التجديد، فالردّ واحد في الحالتين</summary>
        private async Task<LoginResponse> IssueAsync(User user, DateTime sessionStartedAt, DateTime expiresAt)
        {
            var permissions = await _context.RolePermissions
                .Where(rp => rp.RoleId == user.RoleId)
                .Select(rp => rp.Permission.Name)
                .ToListAsync();

            var token = _jwtService.GenerateToken(user, expiresAt);
            await _context.UserTokens.AddAsync(new UserToken
            {
                UserId = user.Id,
                Token = token,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                SessionStartedAt = sessionStartedAt,
                IsRevoked = false
            });
            await _context.SaveChangesAsync();

            return new LoginResponse
            {
                Token = token,
                ExpiresAt = expiresAt,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role.Name,
                BranchId = user.BranchId,
                DepartmentId = user.DepartmentId,
                OfficeId = user.OfficeId,
                Permissions = permissions
            };
        }

        public async Task<LoginResponse?> RenewAsync(string token)
        {
            var now = DateTime.UtcNow;
            var current = await _context.UserTokens.FirstOrDefaultAsync(t => t.Token == token);
            if (current == null || current.IsRevoked || current.ExpiresAt <= now) return null;

            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == current.UserId);
            if (user == null || !user.IsActive) return null;

            var started = current.SessionStartedAt ?? current.CreatedAt;
            if (!SessionRules.CanRenew(now, started, MaxSessionHours)) return null;

            var issued = await IssueAsync(user, started, SessionRules.NextExpiry(now, DurationMinutes, started, MaxSessionHours));

            // التوكن القديم يبقى دقيقتين فقط ثم يموت: طلبات الواجهة التي انطلقت به لا تسقط، ولا يعيش توكنان طويلاً
            current.ExpiresAt = SessionRules.OldTokenExpiry(now, current.ExpiresAt);
            await _context.SaveChangesAsync();
            return issued;
        }

        public async Task<bool> RegisterAsync(RegisterRequest request)
        {
            // التحقق من عدم وجود البريد
            var exists = await _context.Users.AnyAsync(u => u.Email == request.Email);
            if (exists) return false;
            if (PasswordRules.Problem(request.Password, request.Email) is { } weak)
                throw new InvalidOperationException(weak);

            var branchExists = await _context.Branches.AnyAsync(b => b.Id == request.BranchId);
            var department = await _context.Departments.FirstOrDefaultAsync(d => d.Id == request.DepartmentId);
            var office = await _context.Offices.FirstOrDefaultAsync(o => o.Id == request.OfficeId);
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId);

            if (!branchExists || department == null || office == null || role == null)
                return false;

            if (department.BranchId != request.BranchId)
                return false;

            if (office.DepartmentId != request.DepartmentId)
                return false;

            var me = await _currentUserService.GetUserAsync();
            if (!OrganizationRole.IsSystemAdmin(me))
            {
                if (!OrganizationRole.InBranch(me, request.BranchId))
                    return false;

                if (role.Name.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
                    return false;

                var roleHasBranchManagement = await _context.RolePermissions
                    .Include(rp => rp.Permission)
                    .AnyAsync(rp => rp.RoleId == request.RoleId
                                 && (rp.Permission.Name == "CreateBranch" || rp.Permission.Name == "EditBranch" || rp.Permission.Name == "DeleteBranch"));

                if (roleHasBranchManagement)
                    return false;
            }

            var user = new User
            {
                FullName = request.FullName,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                RoleId = request.RoleId,
                DepartmentId = request.DepartmentId,
                OfficeId = request.OfficeId,
                BranchId = request.BranchId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Users.AddAsync(user);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> LogoutAsync(string token)
        {
            var userToken = await _context.UserTokens
                .FirstOrDefaultAsync(t => t.Token == token);

            if (userToken == null) return false;

            userToken.IsRevoked = true;
            return (await _context.SaveChangesAsync()) > 0;
        }
    }
}
