using Microsoft.AspNetCore.Authorization;

namespace API.Authorization
{
    /// <summary>تكفي واحدة من الصلاحيات المذكورة (صلاحية واحدة في الحالة المعتادة)</summary>
    public class PermissionRequirement : IAuthorizationRequirement
    {
        public PermissionRequirement(params string[] permissionNames)
        {
            PermissionNames = permissionNames;
        }

        public IReadOnlyList<string> PermissionNames { get; }
    }
}
