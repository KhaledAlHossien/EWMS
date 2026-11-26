using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Enums;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.RemoveLink
{
    public class RemoveTaskLinkCommandHandler : IRequestHandler<RemoveTaskLinkCommand, List<TaskLinkDto>>
    {
        private readonly IAssignedTaskPlanningService _planning;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly TaskLinksSupport _support;

        public RemoveTaskLinkCommandHandler(IAssignedTaskPlanningService planning, IUserService userService, IUserPermissionService permissions, TaskLinksSupport support)
        {
            _planning = planning;
            _userService = userService;
            _permissions = permissions;
            _support = support;
        }

        public async Task<List<TaskLinkDto>> Handle(RemoveTaskLinkCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var link = await _planning.GetLinkAsync(request.LinkId) ?? throw new KeyNotFoundException("الرابط غير موجود");
            var task = await _support.ViewableTaskAsync(link.AssignedTaskId, viewer);
            if (link.CreatedByUserId != viewer.Id && task.CreatedByUserId != viewer.Id && !viewer.IsSuperAdmin)
                throw new UnauthorizedAccessException("يحذف الرابط من أضافه أو من أسند المهمة");
            if (task.Status == AssignedTaskStatus.Done) throw new InvalidOperationException("لا يمكن تعديل روابط مهمة منجزة");
            AssignedTaskRules.EnsureNotFrozen(task, viewer);
            await _planning.DeleteLinkAsync(link);
            return await _support.ListAsync(task, viewer);
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
