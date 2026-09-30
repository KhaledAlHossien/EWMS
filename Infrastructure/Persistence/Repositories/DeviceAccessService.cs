using Application.Common;
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
            if (string.IsNullOrWhiteSpace(role)) return new DeviceAccess(false, false, false, false, false);

            // صلاحيات الدور (تُمنح من صفحة الأدوار) — SuperAdmin يملكها افتراضياً
            var deviceNames = AppPermissions.DeviceInventory.ToArray();
            var permissions = await _context.RolePermissions
                .Where(rp => rp.Role.Name == role && deviceNames.Contains(rp.Permission.Name))
                .Select(rp => rp.Permission.Name)
                .ToListAsync();

            // القسم من التوكن (يُحدَّث عند إعادة تسجيل الدخول)
            var inOwnerDepartment = _ownerDepartmentId > 0 && _currentUser.DepartmentId == _ownerDepartmentId;

            // رئيس القسم المالك يملك كل العمليات بحكم منصبه
            var isOwnerHead = inOwnerDepartment && role == "Manager";

            var canCreate = isOwnerHead || permissions.Contains("CreateDevice");
            var canEdit = isOwnerHead || permissions.Contains("EditDevice");
            var canDelete = isOwnerHead || permissions.Contains("DeleteDevice");

            // من يستطيع التعديل يجب أن يرى ما يعدّله
            var canView = inOwnerDepartment || permissions.Contains("ViewDevices") || canCreate || canEdit || canDelete;

            return new DeviceAccess(canView, canCreate, canEdit, canDelete, inOwnerDepartment);
        }
    }
}
