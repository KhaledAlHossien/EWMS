using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace API.Authorization
{
    /// <summary>
    /// يفحص صلاحيات دور المستخدم الحالي من قاعدة البيانات (بمعرّف المستخدم لا باسم الدور في التوكن)،
    /// فتغيير دور المستخدم أو صلاحيات دوره يُطبَّق فوراً بلا إعادة تسجيل دخول.
    /// </summary>
    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly IUserPermissionService _permissions;

        public PermissionAuthorizationHandler(IUserPermissionService permissions)
        {
            _permissions = permissions;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            if (!int.TryParse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
                return;

            var granted = await _permissions.GetAsync(userId);
            if (requirement.PermissionNames.Any(granted.Contains))
                context.Succeed(requirement);
        }
    }
}
