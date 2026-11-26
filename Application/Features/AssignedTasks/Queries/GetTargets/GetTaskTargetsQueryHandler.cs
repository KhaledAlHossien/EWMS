using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Enums;
using MediatR;

namespace Application.Features.AssignedTasks.Queries.GetTargets
{
    public class GetTaskTargetsQueryHandler : IRequestHandler<GetTaskTargetsQuery, List<TaskTargetOptionDto>>
    {
        private readonly IAssignedTaskService _taskService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly IDepartmentService _departmentService;
        private readonly IOfficeService _officeService;

        public GetTaskTargetsQueryHandler(IAssignedTaskService taskService, IUserService userService, IUserPermissionService permissions, IDepartmentService departmentService, IOfficeService officeService)
        {
            _taskService = taskService;
            _userService = userService;
            _permissions = permissions;
            _departmentService = departmentService;
            _officeService = officeService;
        }

        public async Task<List<TaskTargetOptionDto>> Handle(GetTaskTargetsQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var user = viewer.User;

            // التفويض: الجهات أدنى بدرجة ضمن وحدة المهمة الأصل
            if (request.ParentTaskId is int parentId)
            {
                var parent = await _taskService.GetByIdAsync(parentId)
                    ?? throw new KeyNotFoundException("المهمة غير موجودة");
                if (!AssignedTaskRules.CanHandle(parent, viewer))
                    throw new UnauthorizedAccessException("يمكنك تفويض المهام الواردة إليك فقط");

                return AssignedTaskRules.DelegationTargetFor(parent) switch
                {
                    AssignedTaskTargetType.Office when parent.DepartmentId is int d => await OfficesAsync(d),
                    AssignedTaskTargetType.User when parent.OfficeId is int o => await UsersAsync(o, user.Id),
                    _ => []
                };
            }

            var types = AssignedTaskRules.TargetTypesFor(viewer);
            var type = string.IsNullOrWhiteSpace(request.Type)
                ? types.Cast<AssignedTaskTargetType?>().FirstOrDefault()
                : Enum.TryParse<AssignedTaskTargetType>(request.Type, true, out var parsed) && types.Contains(parsed) ? parsed
                : throw new UnauthorizedAccessException("لا تملك صلاحية الإسناد لهذا النوع من الجهات");

            return type switch
            {
                AssignedTaskTargetType.Department when user.BranchId is int b => await DepartmentsAsync(b),
                AssignedTaskTargetType.Office when user.DepartmentId is int d => await OfficesAsync(d),
                AssignedTaskTargetType.User when user.OfficeId is int o => await UsersAsync(o, user.Id),
                _ => []
            };
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_userService, _permissions);

        private async Task<List<TaskTargetOptionDto>> DepartmentsAsync(int branchId)
        {
            var handlers = await _permissions.GetUsersWithPermissionAsync(AppPermissions.HandleUnitTasks, branchId: branchId);
            return (await _departmentService.GetAllAsync())
                .Where(d => d.BranchId == branchId).OrderBy(d => d.Name)
                .Select(d => new TaskTargetOptionDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    HeadNames = string.Join("، ", handlers
                        .Where(u => OrganizationRole.IsInDepartmentItself(u, d.Id)).Select(u => u.FullName))
                }).ToList();
        }

        private async Task<List<TaskTargetOptionDto>> OfficesAsync(int departmentId)
        {
            var handlers = await _permissions.GetUsersWithPermissionAsync(AppPermissions.HandleUnitTasks, departmentId: departmentId);
            return (await _officeService.GetByDepartmentAsync(departmentId))
                .OrderBy(o => o.Name)
                .Select(o => new TaskTargetOptionDto
                {
                    Id = o.Id,
                    Name = o.Name,
                    HeadNames = string.Join("، ", handlers.Where(u => u.OfficeId == o.Id).Select(u => u.FullName))
                }).ToList();
        }

        private async Task<List<TaskTargetOptionDto>> UsersAsync(int officeId, int excludeUserId) =>
            (await _userService.GetByOfficeAsync(officeId))
                .Where(u => u.IsActive && u.Id != excludeUserId).OrderBy(u => u.FullName)
                .Select(u => new TaskTargetOptionDto { Id = u.Id, Name = u.FullName })
                .ToList();
    }
}
