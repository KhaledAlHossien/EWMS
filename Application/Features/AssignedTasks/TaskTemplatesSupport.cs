using Application.Common;
using Application.Interfaces;

namespace Application.Features.AssignedTasks
{
    /// <summary>الدوال المشتركة بين عمليات هذه الميزة</summary>
    public class TaskTemplatesSupport
    {
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public TaskTemplatesSupport(IUserService userService, IUserPermissionService permissions)
        {
            _userService = userService;
            _permissions = permissions;
        }

        public async Task<Viewer> AssignerAsync()
        {
            var viewer = await Viewer.CurrentAsync(_userService, _permissions);
            if (AssignedTaskRules.TargetTypesFor(viewer).Count == 0)
                throw new UnauthorizedAccessException("القوالب والمهام الدورية لمن يملك صلاحية إسناد المهام");
            return viewer;
        }
    }
}
