using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace API.Authorization
{
    /// <summary>
    /// سياسات ViewDevices / CreateDevice / EditDevice / DeleteDevice: الصلاحية من الدور أو الانتماء لقسم العمليات
    /// (المنطق كاملاً في IDeviceAccessService حتى تتطابق الواجهة (Devices/MyAccess) مع الحماية).
    /// </summary>
    public class DeviceAccessRequirement : IAuthorizationRequirement
    {
        public DeviceAccessRequirement(DeviceOperation operation)
        {
            Operation = operation;
        }

        public DeviceOperation Operation { get; }
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
            if (access.Can(requirement.Operation))
                context.Succeed(requirement);
        }
    }
}
