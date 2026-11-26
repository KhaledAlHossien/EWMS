using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Enums;
using MediatR;

namespace Application.Features.AssignedTasks.Queries.GetBoard
{
    public class GetTaskBoardQueryHandler : IRequestHandler<GetTaskBoardQuery, TaskBoardDto>
    {
        private readonly IAssignedTaskService _taskService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public GetTaskBoardQueryHandler(IAssignedTaskService taskService, IUserService userService, IUserPermissionService permissions)
        {
            _taskService = taskService;
            _userService = userService;
            _permissions = permissions;
        }

        public async Task<TaskBoardDto> Handle(GetTaskBoardQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var mode = (request.Mode ?? "incoming").ToLowerInvariant();
            var filter = AssignedTaskRules.ModeFilter(viewer, mode);

            // المنجزة: آخر 30 يوماً افتراضياً، ويمكن توسيعها (حتى 10 سنوات) من فلتر اللوحة
            var doneSince = DateTime.UtcNow.AddDays(-Math.Clamp(request.DoneDays ?? 30, 1, 3650));
            var tasks = await _taskService.GetBoardAsync(filter);
            var targetTypes = AssignedTaskRules.TargetTypesFor(viewer);

            return new TaskBoardDto
            {
                Mode = mode is "outgoing" or "scope" ? mode : "incoming",
                CanCreate = targetTypes.Count > 0,
                TargetTypeLabel = AssignedTaskRules.TargetTypeLabel(targetTypes.FirstOrDefault()),
                TargetTypes = targetTypes.Select(t => new TaskTargetTypeDto
                {
                    Value = t.ToString(),
                    Label = AssignedTaskRules.TargetTypeLabel(t)
                }).ToList(),
                HasScope = AssignedTaskRules.HasScope(viewer),
                Tasks = tasks
                    .Where(t => t.Status != AssignedTaskStatus.Done || (t.CompletedAt ?? t.UpdatedAt) >= doneSince)
                    .Select(t => AssignedTaskRules.ToCard(t, viewer))
                    .ToList()
            };
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
