using Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace API.Authorization
{
    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly DataContext _context;

        public PermissionAuthorizationHandler(DataContext context)
        {
            _context = context;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            var roleName = context.User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.IsNullOrWhiteSpace(roleName))
                return;

            var hasPermission = await _context.RolePermissions
                .Include(rp => rp.Role)
                .Include(rp => rp.Permission)
                .AnyAsync(rp => rp.Role.Name == roleName
                             && rp.Permission.Name == requirement.PermissionName);

            if (hasPermission)
                context.Succeed(requirement);
        }
    }
}
