using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace API.Authorization
{
    /// <summary>
    /// سياستا ViewDevices / ManageDevices: الصلاحية من الدور أو الانتماء لقسم العمليات
    /// (المنطق كاملاً في IDeviceAccessService حتى تتطابق الواجهة (Devices/MyAccess) مع الحماية).
    /// </summary>
    public class DeviceAccessRequirement : IAuthorizationRequirement
    {
        public DeviceAccessRequirement(bool manage)
        {
            Manage = manage;
        }

        public bool Manage { get; }
    }

    public class DeviceAccessAuthorizationHandler : AuthorizationHandler<DeviceAccessRequirement>
    {
        private readonly IDeviceAccessService _access;

        public DeviceAccessAuthorizationHandler(IDeviceAccessService access)
        {
            _access = access;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            DeviceAccessRequirement requirement)
        {
            var access = await _access.GetCurrentAsync();
            if (requirement.Manage ? access.CanManage : access.CanView)
                context.Succeed(requirement);
        }
    }
}
