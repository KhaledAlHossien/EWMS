using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.Create
{
    public class CreateAssignedTaskCommandHandler : IRequestHandler<CreateAssignedTaskCommand, AssignedTaskDetailDto>
    {
        private readonly IUserService _userService;
        private readonly AssignedTaskCreator _creator;
        private readonly IUserPermissionService _permissions;

        public CreateAssignedTaskCommandHandler(IUserService userService, IUserPermissionService permissions, AssignedTaskCreator creator)
        {
            _userService = userService;
            _creator = creator;
            _permissions = permissions;
        }

        public async Task<AssignedTaskDetailDto> Handle(CreateAssignedTaskCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var dto = request.Dto;
            var created = await _creator.CreateAsync(viewer, new NewTaskSpec(
                dto.Title, dto.Description, dto.Priority, dto.DueDate, dto.TargetType, dto.TargetId, dto.ParentTaskId, dto.ChecklistItems));
            return AssignedTaskRules.ToDetail(created, viewer);
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
