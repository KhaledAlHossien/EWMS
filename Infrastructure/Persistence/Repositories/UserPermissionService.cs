using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class UserPermissionService : IUserPermissionService
    {
        private readonly DataContext _context;
        private readonly Dictionary<int, IReadOnlySet<string>> _cache = new();

        public UserPermissionService(DataContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlySet<string>> GetAsync(int userId)
        {
            if (_cache.TryGetValue(userId, out var cached)) return cached;

            var names = await _context.Users
                .Where(u => u.Id == userId && u.IsActive)
                .SelectMany(u => _context.RolePermissions.Where(rp => rp.RoleId == u.RoleId))
                .Select(rp => rp.Permission.Name)
                .ToListAsync();

            var set = names.ToHashSet(StringComparer.Ordinal);
            _cache[userId] = set;
            return set;
        }

        public async Task<bool> HasAsync(int userId, string permission) =>
            (await GetAsync(userId)).Contains(permission);

        public async Task<List<User>> GetUsersWithPermissionAsync(
            string permission, int? branchId = null, int? departmentId = null, int? officeId = null, bool exactUnit = false)
        {
            var query = _context.Users
                .Include(u => u.Role)
                .Where(u => u.IsActive && _context.RolePermissions
                    .Any(rp => rp.RoleId == u.RoleId && rp.Permission.Name == permission));

            if (branchId != null) query = query.Where(u => u.BranchId == branchId);
            if (departmentId != null) query = query.Where(u => u.DepartmentId == departmentId);
            if (officeId != null) query = query.Where(u => u.OfficeId == officeId);

            if (exactUnit)
            {
                if (officeId != null) { /* المكتب أدنى وحدة */ }
                else if (departmentId != null) query = query.Where(u => u.OfficeId == null);
                else if (branchId != null) query = query.Where(u => u.DepartmentId == null);
            }

            return await query.ToListAsync();
        }
    }
}
