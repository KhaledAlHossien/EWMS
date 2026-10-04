using Application.Common;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence.Repositories
{
    public class DeviceAccessService : IDeviceAccessService
    {
        private readonly IUserPermissionService _permissions;
        private readonly ICurrentUserService _currentUser;
        private readonly int _ownerDepartmentId;

        public DeviceAccessService(IUserPermissionService permissions, ICurrentUserService currentUser, IConfiguration configuration)
        {
            _permissions = permissions;
            _currentUser = currentUser;
            _ownerDepartmentId = configuration.GetValue<int>("DeviceInventory:OwnerDepartmentId");
        }

        public async Task<DeviceAccess> GetCurrentAsync()
        {
            if (_currentUser.UserId <= 0) return new DeviceAccess(false, false, false, false, false);

            // كل عملية تحددها صلاحيات دور المستخدم فقط (Role-Permission) — لا صلاحية ضمنية للقسم المالك ولا لمنصب
            var permissions = await _permissions.GetAsync(_currentUser.UserId);

            var canCreate = permissions.Contains("CreateDevice");
            var canEdit = permissions.Contains("EditDevice");
            var canDelete = permissions.Contains("DeleteDevice");

            // من يستطيع التعديل يجب أن يرى ما يعدّله
            var canView = permissions.Contains("ViewDevices") || canCreate || canEdit || canDelete;

            // القسم المالك يحدد فقط أين تظهر اختصارات التوثيق في لوحات المتابعة (من التوكن)
            var inOwnerDepartment = _ownerDepartmentId > 0 && _currentUser.DepartmentId == _ownerDepartmentId;

            return new DeviceAccess(canView, canCreate, canEdit, canDelete, inOwnerDepartment);
        }
    }
}
