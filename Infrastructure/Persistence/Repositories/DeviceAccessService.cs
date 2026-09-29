using Application.Interfaces;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence.Repositories
{
    public class DeviceAccessService : IDeviceAccessService
    {
        private readonly DataContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly int _ownerDepartmentId;

        public DeviceAccessService(DataContext context, ICurrentUserService currentUser, IConfiguration configuration)
        {
            _context = context;
            _currentUser = currentUser;
            _ownerDepartmentId = configuration.GetValue<int>("DeviceInventory:OwnerDepartmentId");
        }

        public async Task<DeviceAccess> GetCurrentAsync()
        {
            var role = _currentUser.Role;
            if (string.IsNullOrWhiteSpace(role)) return new DeviceAccess(false, false, false);

            // صلاحيات الدور (تُمنح من صفحة الأدوار) — SuperAdmin يملكها افتراضياً
            var permissions = await _context.RolePermissions
                .Where(rp => rp.Role.Name == role
                          && (rp.Permission.Name == "ViewDevices" || rp.Permission.Name == "ManageDevices"))
                .Select(rp => rp.Permission.Name)
                .ToListAsync();

            // القسم من التوكن (يُحدَّث عند إعادة تسجيل الدخول)
            var inOwnerDepartment = _ownerDepartmentId > 0 && _currentUser.DepartmentId == _ownerDepartmentId;

            var canManage = permissions.Contains("ManageDevices") || (inOwnerDepartment && role == "Manager");
            var canView = canManage || permissions.Contains("ViewDevices") || inOwnerDepartment;

            return new DeviceAccess(canView, canManage, inOwnerDepartment);
        }
    }
}
