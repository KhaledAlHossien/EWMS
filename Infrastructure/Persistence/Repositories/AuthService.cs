using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
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

        public AuthService(
            DataContext context,
            IJwtService jwtService,
            ICurrentUserService currentUserService)
        {
            _context = context;
            _jwtService = jwtService;
            _currentUserService = currentUserService;
        }

        public async Task<LoginResponse?> LoginAsync(LoginRequest request)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Department)
                .Include(u => u.Branch)
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null || !user.IsActive) return null;

            // التحقق من كلمة المرور باستخدام BCrypt
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return null;

            // توليد التوكن
            var token = _jwtService.GenerateToken(user);
            var expiresAt = _jwtService.GetExpirationDate();

            // حفظ التوكن في قاعدة البيانات (لإبطاله لاحقاً)
            var userToken = new UserToken
            {
                UserId = user.Id,
                Token = token,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                IsRevoked = false
            };

            await _context.UserTokens.AddAsync(userToken);
            await _context.SaveChangesAsync();

            return new LoginResponse
            {
                Token = token,
                ExpiresAt = expiresAt,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role.Name
            };
        }

        public async Task<bool> RegisterAsync(RegisterRequest request)
        {
            // التحقق من عدم وجود البريد
            var exists = await _context.Users.AnyAsync(u => u.Email == request.Email);
            if (exists) return false;

            var branchExists = await _context.Branches.AnyAsync(b => b.Id == request.BranchId);
            var department = await _context.Departments.FirstOrDefaultAsync(d => d.Id == request.DepartmentId);
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId);

            if (!branchExists || department == null || role == null)
                return false;

            if (department.BranchId != request.BranchId)
                return false;

            if (!_currentUserService.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
            {
                if (_currentUserService.BranchId != request.BranchId)
                    return false;

                if (role.Name.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
                    return false;

                var roleHasBranchManagement = await _context.RolePermissions
                    .Include(rp => rp.Permission)
                    .AnyAsync(rp => rp.RoleId == request.RoleId
                                 && rp.Permission.Name == "ManageBranches");

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
