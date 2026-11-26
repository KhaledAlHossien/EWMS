using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.AddComment
{
    public class AddAssignedTaskCommentCommandHandler : IRequestHandler<AddAssignedTaskCommentCommand, AssignedTaskDetailDto>
    {
        private readonly IAssignedTaskService _taskService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly INotificationService _notificationService;

        public AddAssignedTaskCommentCommandHandler(IAssignedTaskService taskService, IUserService userService, IUserPermissionService permissions, INotificationService notificationService)
        {
            _taskService = taskService;
            _userService = userService;
            _permissions = permissions;
            _notificationService = notificationService;
        }

        public async Task<AssignedTaskDetailDto> Handle(AddAssignedTaskCommentCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var user = viewer.User;
            var task = await LoadAsync(request.Id);

            if (!AssignedTaskRules.CanView(task, viewer))
                throw new UnauthorizedAccessException("لا تملك صلاحية التعليق على هذه المهمة");

            var text = request.Text.Trim();
            await _taskService.AddActivityAsync(AssignedTaskRules.Activity(task, user, AssignedTaskActivityType.Comment, text));

            // المُسنِد + المنفِّذون (عدا كاتب التعليق)
            var recipients = (await AssignedTaskRules.HandlerUserIdsAsync(_permissions, task)).Append(task.CreatedByUserId);
            await AssignedTaskRules.NotifyAsync(_notificationService, recipients, user.Id, task,
                NotificationType.TaskCommented, "تعليق جديد على مهمة",
                $"{user.FullName} على «{task.Title}»: {(text.Length > 80 ? text[..80] + "…" : text)}");

            return AssignedTaskRules.ToDetail(await LoadAsync(task.Id), viewer);
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_userService, _permissions);

        private async Task<AssignedTask> LoadAsync(int id) =>
            await _taskService.GetByIdAsync(id) ?? throw new KeyNotFoundException("المهمة غير موجودة");
    }
}
