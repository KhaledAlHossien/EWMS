using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Infrastructure.Persistence.Repositories
{
    public class UserService : IUserService
    {
        private readonly DataContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserService(DataContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        // المستخدم الحالي من التوكن
        public int UserId
        {
            get
            {
                var userId = _httpContextAccessor.HttpContext?
                    .User?
                    .FindFirst(ClaimTypes.NameIdentifier)?
                    .Value;

                return string.IsNullOrEmpty(userId) ? 0 : int.Parse(userId);
            }
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            return await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Department)
                .Include(u => u.Branch)
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<List<User>> GetAllAsync()
        {
            return await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Department)
                .Include(u => u.Branch)
                .ToListAsync();
        }

        public async Task AddAsync(User user)
        {
            await _context.Users.AddAsync(user);
            await SaveChangesAsync();
        }

        public async Task UpdateAsync(User user)
        {
            _context.Users.Update(user);
            await SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(User user)
        {
            _context.Users.Remove(user);
            return await SaveChangesAsync();
        }

        public async Task<bool> SaveChangesAsync()
        {
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Department)
                .Include(u => u.Branch)
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User?> GetWithDetailsAsync(int id)
        {
            return await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Department)
                .Include(u => u.Branch)
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<bool> IsEmailUniqueAsync(string email, int? excludeId = null)
        {
            return !await _context.Users
                .AnyAsync(u => u.Email == email && u.Id != excludeId);
        }

        public async Task<List<User>> GetByDepartmentAsync(int departmentId)
        {
            return await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Department)
                .Include(u => u.Branch)
                .Where(u => u.DepartmentId == departmentId)
                .ToListAsync();
        }

        public async Task<List<User>> GetByBranchAsync(int branchId)
        {
            return await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Department)
                .Include(u => u.Branch)
                .Where(u => u.BranchId == branchId)
                .ToListAsync();
        }

        public async Task<bool> IsUserUsedAsync(int userId)
        {
            // نتحقق إن كان المستخدم مرتبطاً بأي مشروع أو تعيين أو تحويل أو ملف
            var usedInProjects = await _context.Projects.AnyAsync(p => p.CreatedById == userId);
            var usedInAssignments = await _context.ProjectAssignments
                .AnyAsync(pa => pa.AssignedUserId == userId || pa.AssignedByUserId == userId);
            var usedInTransfers = await _context.ProjectTransfers
                .AnyAsync(pt => pt.TransferredById == userId);
            var usedInFiles = await _context.ProjectFiles
                .AnyAsync(pf => pf.UploadedById == userId);

            return usedInProjects || usedInAssignments || usedInTransfers || usedInFiles;
        }
    }
}
