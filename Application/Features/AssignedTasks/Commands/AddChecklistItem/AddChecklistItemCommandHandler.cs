using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.AddChecklistItem
{
    public class AddChecklistItemCommandHandler : IRequestHandler<AddChecklistItemCommand, AssignedTaskDetailDto>
    {
        private readonly IAssignedTaskService _tasks;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly AssignedTaskExtrasSupport _support;

        public AddChecklistItemCommandHandler(IAssignedTaskService tasks, IUserService users, IUserPermissionService permissions, AssignedTaskExtrasSupport support)
        {
            _tasks = tasks;
            _users = users;
            _permissions = permissions;
            _support = support;
        }

        public async Task<AssignedTaskDetailDto> Handle(AddChecklistItemCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var task = await _support.LoadAsync(request.TaskId);
            AssignedTaskExtrasSupport.EnsureCanManageChecklist(task, viewer);

            if (await _tasks.CountChecklistAsync(task.Id) >= AssignedTaskRules.MaxChecklistItems)
                throw new InvalidOperationException($"الحد الأقصى {AssignedTaskRules.MaxChecklistItems} بنداً في القائمة");

            await _tasks.AddChecklistItemAsync(new AssignedTaskChecklistItem
            {
                AssignedTaskId = task.Id,
                Text = request.Text.Trim(),
                CreatedAt = DateTime.UtcNow
            });
            return await _support.DetailAsync(task.Id, viewer);
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
