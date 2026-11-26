using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Enums;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.Claim
{
    public class ClaimAssignedTaskCommandHandler : IRequestHandler<ClaimAssignedTaskCommand, AssignedTaskDetailDto>
    {
        private readonly IAssignedTaskService _tasks;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly INotificationService _notifications;
        private readonly AssignedTaskExtrasSupport _support;

        public ClaimAssignedTaskCommandHandler(IAssignedTaskService tasks, IUserService users, IUserPermissionService permissions, INotificationService notifications, AssignedTaskExtrasSupport support)
        {
            _tasks = tasks;
            _users = users;
            _permissions = permissions;
            _notifications = notifications;
            _support = support;
        }

        public async Task<AssignedTaskDetailDto> Handle(ClaimAssignedTaskCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var user = viewer.User;
            var task = await _support.LoadAsync(request.TaskId);
            AssignedTaskExtrasSupport.EnsureOpen(task, viewer);

            var now = DateTime.UtcNow;
            if (request.Claim)
            {
                if (task.TargetType == AssignedTaskTargetType.User)
                    throw new InvalidOperationException("مهمة الموظف له وحده — لا حاجة لتوليها");
                if (!AssignedTaskRules.CanHandle(task, viewer))
                    throw new UnauthorizedAccessException("تتولّى المهمة الجهة المنفِّذة فقط");
                if (task.ClaimedByUserId == user.Id) return await _support.DetailAsync(task.Id, viewer);

                var previous = task.ClaimedByUser?.FullName;
                task.ClaimedByUserId = user.Id;
                task.ClaimedAt = now;
                task.UpdatedAt = now;
                await _tasks.SaveChangesAsync();
                await _tasks.AddActivityAsync(AssignedTaskRules.Activity(task, user, AssignedTaskActivityType.Claimed,
                    previous == null ? "تولّى العمل على المهمة" : $"تولّى العمل على المهمة (بدل {previous})"));
                await AssignedTaskRules.NotifyAsync(_notifications, [task.CreatedByUserId], user.Id, task,
                    NotificationType.TaskStatusChanged, "تولّى موظف مهمة", $"«{task.Title}»: يتولاها {user.FullName}");
            }
            else
            {
                if (task.ClaimedByUserId == null) return await _support.DetailAsync(task.Id, viewer);
                if (task.ClaimedByUserId != user.Id && !AssignedTaskRules.IsReviewer(task, viewer))
                    throw new UnauthorizedAccessException("يتخلّى عن المهمة من تولّاها أو من أسندها");

                task.ClaimedByUserId = null;
                task.ClaimedAt = null;
                task.UpdatedAt = now;
                await _tasks.SaveChangesAsync();
                await _tasks.AddActivityAsync(AssignedTaskRules.Activity(task, user, AssignedTaskActivityType.Released, "أخلى المهمة (لم يعد يتولاها أحد)"));
            }

            return await _support.DetailAsync(task.Id, viewer);
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
