using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.UpdateChecklistItem
{
    public class UpdateChecklistItemCommandHandler : IRequestHandler<UpdateChecklistItemCommand, AssignedTaskDetailDto>
    {
        private readonly IAssignedTaskService _tasks;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly AssignedTaskExtrasSupport _support;

        public UpdateChecklistItemCommandHandler(IAssignedTaskService tasks, IUserService users, IUserPermissionService permissions, AssignedTaskExtrasSupport support)
        {
            _tasks = tasks;
            _users = users;
            _permissions = permissions;
            _support = support;
        }

        public async Task<AssignedTaskDetailDto> Handle(UpdateChecklistItemCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var item = await _tasks.GetChecklistItemAsync(request.ItemId) ?? throw new KeyNotFoundException("البند غير موجود");
            var task = await _support.LoadAsync(item.AssignedTaskId);
            AssignedTaskExtrasSupport.EnsureCanManageChecklist(task, viewer);

            if (request.Text != null)
            {
                // تعديل النص للمُسنِد وحده؛ المنفِّذ يعلّم فقط
                if (!AssignedTaskRules.IsReviewer(task, viewer))
                    throw new UnauthorizedAccessException("يعدّل نص البند من أسند المهمة");
                item.Text = request.Text.Trim();
            }
            if (request.IsDone is bool done && done != item.IsDone)
            {
                item.IsDone = done;
                item.DoneAt = done ? DateTime.UtcNow : null;
            }
            await _tasks.SaveChangesAsync();
            return await _support.DetailAsync(task.Id, viewer);
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
