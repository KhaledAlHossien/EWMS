using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.ChangeStatus
{
    public class ChangeAssignedTaskStatusCommandHandler : IRequestHandler<ChangeAssignedTaskStatusCommand, AssignedTaskCardDto>
    {
        private readonly IAssignedTaskService _taskService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly INotificationService _notificationService;

        public ChangeAssignedTaskStatusCommandHandler(IAssignedTaskService taskService, IUserService userService, IUserPermissionService permissions, INotificationService notificationService)
        {
            _taskService = taskService;
            _userService = userService;
            _permissions = permissions;
            _notificationService = notificationService;
        }

        // الانتقالات المسموحة تحددها AssignedTaskRules.AllowedStatuses: المنفِّذ يرسل للمراجعة، والمُسنِد يعتمد أو يعيد بسبب.
        public async Task<AssignedTaskCardDto> Handle(ChangeAssignedTaskStatusCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var user = viewer.User;
            var task = await LoadAsync(request.Id);

            var from = task.Status;
            var to = (AssignedTaskStatus)request.Status;
            // قرار بُني على حالة قديمة (أعادها غيره أو اعتمدها قبل لحظات): يُرفض بدل أن يكتب فوقها
            if (request.ExpectedStatus is int expected && (AssignedTaskStatus)expected != from)
                throw new InvalidOperationException($"تغيّرت حالة المهمة إلى «{AssignedTaskRules.StatusAr(from)}» منذ فتحتها، حدّث الصفحة وحاول مجدداً");
            if (from == to) return AssignedTaskRules.ToCard(task, viewer);

            var handles = AssignedTaskRules.CanHandle(task, viewer);
            var reviewer = AssignedTaskRules.IsReviewer(task, viewer);
            if (!handles && !reviewer)
                throw new UnauthorizedAccessException("تغيير حالة المهمة للجهة المنفِّذة أو لمن أسندها فقط");
            if (from == AssignedTaskStatus.Done)
                throw new InvalidOperationException("المهمة منجزة ولا يمكن تغيير حالتها");
            AssignedTaskRules.EnsureNotFrozen(task, viewer);
            if (!AssignedTaskRules.AllowedStatuses(task, viewer).Contains(to))
                throw new InvalidOperationException(to == AssignedTaskStatus.Done && handles && !reviewer
                    ? "تُرسَل المهمة إلى «بانتظار المراجعة» ويعتمد إنجازها من أسندها"
                    : $"لا يمكنك نقل المهمة من «{AssignedTaskRules.StatusAr(from)}» إلى «{AssignedTaskRules.StatusAr(to)}»");

            var note = (request.Note ?? string.Empty).Trim();
            var returned = from == AssignedTaskStatus.InReview && to == AssignedTaskStatus.InProgress && AssignedTaskRules.NeedsReturnNote(task, viewer);
            if (returned && note.Length == 0)
                throw new InvalidOperationException("اكتب سبب إعادة المهمة إلى التنفيذ");

            var now = DateTime.UtcNow;
            task.Status = to;
            task.UpdatedAt = now;
            if (returned) task.ReturnCount++;
            if (to == AssignedTaskStatus.InProgress) task.StartedAt ??= now;
            task.CompletedAt = to == AssignedTaskStatus.Done ? now : null;

            // بدء العمل على مهمة وحدة (قسم/مكتب) يسجّل صاحبه تلقائياً إن لم يتولّها أحد
            var autoClaimed = handles && from == AssignedTaskStatus.Todo && to == AssignedTaskStatus.InProgress
                && task.TargetType != AssignedTaskTargetType.User && task.ClaimedByUserId == null;
            if (autoClaimed) { task.ClaimedByUserId = user.Id; task.ClaimedAt = now; }

            // الحالة وسجلّها في حفظ واحد (AddActivityAsync يحفظ المهمة المعدّلة معه، ويفحص RowVersion)
            var text = $"نقل المهمة من «{AssignedTaskRules.StatusAr(from)}» إلى «{AssignedTaskRules.StatusAr(to)}»"
                + (to == AssignedTaskStatus.Done && reviewer && from == AssignedTaskStatus.InReview ? " (اعتمد الإنجاز)" : "")
                + (returned ? $" — السبب: {note}" : "");
            await _taskService.AddActivityAsync(AssignedTaskRules.Activity(task, user, AssignedTaskActivityType.StatusChanged, text, from, to));

            // الإشعارات: المراجعة ← المُسنِد، الاعتماد/الإعادة ← المنفِّذون، وغير ذلك ← المُسنِد كالسابق
            if (to == AssignedTaskStatus.InReview)
                await AssignedTaskRules.NotifyAsync(_notificationService, [task.CreatedByUserId], user.Id, task,
                    NotificationType.TaskStatusChanged, "مهمة بانتظار مراجعتك",
                    $"«{task.Title}»: أرسلها {user.FullName} للمراجعة");
            else if (to == AssignedTaskStatus.Done)
                await AssignedTaskRules.NotifyAsync(_notificationService, await AssignedTaskRules.HandlerUserIdsAsync(_permissions, task), user.Id, task,
                    NotificationType.TaskStatusChanged, "اعتُمد إنجاز مهمة",
                    $"«{task.Title}»: اعتمد {user.FullName} إنجازها");
            else if (returned)
                await AssignedTaskRules.NotifyAsync(_notificationService, await AssignedTaskRules.HandlerUserIdsAsync(_permissions, task), user.Id, task,
                    NotificationType.TaskStatusChanged, "أُعيدت مهمة للتنفيذ",
                    $"«{task.Title}»: أعادها {user.FullName} — السبب: {note}");
            else
                await AssignedTaskRules.NotifyAsync(_notificationService, [task.CreatedByUserId], user.Id, task,
                    NotificationType.TaskStatusChanged, "تحديث على مهمة",
                    $"«{task.Title}»: {AssignedTaskRules.StatusAr(to)} — بواسطة {user.FullName}");

            return AssignedTaskRules.ToCard(await LoadAsync(task.Id), viewer);
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_userService, _permissions);

        private async Task<AssignedTask> LoadAsync(int id) =>
            await _taskService.GetByIdAsync(id) ?? throw new KeyNotFoundException("المهمة غير موجودة");
    }
}
