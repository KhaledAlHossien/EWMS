using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.DeleteChecklistItem
{
    public class DeleteChecklistItemCommandHandler : IRequestHandler<DeleteChecklistItemCommand, AssignedTaskDetailDto>
    {
        private readonly IAssignedTaskService _tasks;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly AssignedTaskExtrasSupport _support;

        public DeleteChecklistItemCommandHandler(IAssignedTaskService tasks, IUserService users, IUserPermissionService permissions, AssignedTaskExtrasSupport support)
        {
            _tasks = tasks;
            _users = users;
            _permissions = permissions;
            _support = support;
        }

        public async Task<AssignedTaskDetailDto> Handle(DeleteChecklistItemCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var item = await _tasks.GetChecklistItemAsync(request.ItemId) ?? throw new KeyNotFoundException("البند غير موجود");
            var task = await _support.LoadAsync(item.AssignedTaskId);
            AssignedTaskExtrasSupport.EnsureCanManageChecklist(task, viewer);

            await _tasks.DeleteChecklistItemAsync(item);
            return await _support.DetailAsync(task.Id, viewer);
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
