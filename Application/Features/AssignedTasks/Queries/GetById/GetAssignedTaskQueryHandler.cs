using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.AssignedTasks.Queries.GetById
{
    public class GetAssignedTaskQueryHandler : IRequestHandler<GetAssignedTaskQuery, AssignedTaskDetailDto>
    {
        private readonly IAssignedTaskService _taskService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public GetAssignedTaskQueryHandler(IAssignedTaskService taskService, IUserService userService, IUserPermissionService permissions)
        {
            _taskService = taskService;
            _userService = userService;
            _permissions = permissions;
        }

        public async Task<AssignedTaskDetailDto> Handle(GetAssignedTaskQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var task = await _taskService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("المهمة غير موجودة");

            if (!AssignedTaskRules.CanView(task, viewer))
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض هذه المهمة");

            return AssignedTaskRules.ToDetail(task, viewer);
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
