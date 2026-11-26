using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Application.Features.AssignedTasks.Queries.GetByLink
{
    public class GetTasksByLinkQueryHandler : IRequestHandler<GetTasksByLinkQuery, List<AssignedTaskCardDto>>
    {
        private readonly IAssignedTaskService _tasks;
        private readonly IAssignedTaskPlanningService _planning;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly TaskLinkResolver _resolver;

        public GetTasksByLinkQueryHandler(IAssignedTaskService tasks, IAssignedTaskPlanningService planning, IUserService userService, IUserPermissionService permissions, TaskLinkResolver resolver)
        {
            _tasks = tasks;
            _planning = planning;
            _userService = userService;
            _permissions = permissions;
            _resolver = resolver;
        }

        public async Task<List<AssignedTaskCardDto>> Handle(GetTasksByLinkQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            if (!TaskLinkResolver.TryParseType(request.EntityType, out var type)) throw new ArgumentException("نوع السجل غير صحيح");
            // لا يرى المهام المرتبطة إلا من يحق له عرض السجل نفسه
            var described = await _resolver.DescribeAsync(viewer, type, request.EntityId);
            if (!described.Available) return [];

            var ids = await _planning.GetTaskIdsByLinkAsync(type, request.EntityId);
            if (ids.Count == 0) return [];
            return (await _tasks.GetBoardAsync(t => ids.Contains(t.Id)))
                .Where(t => AssignedTaskRules.CanView(t, viewer))
                .Select(t => AssignedTaskRules.ToCard(t, viewer)).ToList();
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
