using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.AssignedTasks
{
    public record CreateAssignedTaskCommand(CreateAssignedTaskRequestDto Dto) : IRequest<AssignedTaskDetailDto>;
    public record UpdateAssignedTaskCommand(int Id, UpdateAssignedTaskRequestDto Dto) : IRequest<AssignedTaskDetailDto>;
    public record DeleteAssignedTaskCommand(int Id) : IRequest<Unit>;
    public record ChangeAssignedTaskStatusCommand(int Id, int Status, string? Note = null, int? ExpectedStatus = null) : IRequest<AssignedTaskCardDto>;
    public record AddAssignedTaskCommentCommand(int Id, string Text) : IRequest<AssignedTaskDetailDto>;

    // ════════════════════ التحقق من المدخلات ════════════════════

    public class CreateAssignedTaskCommandValidator : AbstractValidator<CreateAssignedTaskCommand>
    {
        public CreateAssignedTaskCommandValidator()
        {
            RuleFor(x => x.Dto.Title).NotEmpty().WithMessage("عنوان المهمة مطلوب")
                .MaximumLength(200).WithMessage("العنوان لا يتجاوز 200 حرف");
            RuleFor(x => x.Dto.Description).MaximumLength(4000).WithMessage("الوصف لا يتجاوز 4000 حرف");
            RuleFor(x => x.Dto.Priority).InclusiveBetween(1, 4).WithMessage("الأولوية غير صحيحة");
            RuleFor(x => x.Dto.TargetId).GreaterThan(0).WithMessage("اختر الجهة المُسندة إليها المهمة");
            RuleFor(x => x.Dto.ChecklistItems).Must(l => l == null || l.Count <= AssignedTaskRules.MaxChecklistItems)
                .WithMessage($"بنود التحقق لا تتجاوز {AssignedTaskRules.MaxChecklistItems} بنداً");
            RuleForEach(x => x.Dto.ChecklistItems).MaximumLength(200).WithMessage("البند لا يتجاوز 200 حرف");
            RuleFor(x => x.Dto.ChecklistItems).Must(l => l == null || l.Count <= AssignedTaskRules.MaxChecklistItems)
                .WithMessage($"بنود التحقق لا تتجاوز {AssignedTaskRules.MaxChecklistItems} بنداً");
            RuleForEach(x => x.Dto.ChecklistItems).MaximumLength(200).WithMessage("البند لا يتجاوز 200 حرف");
            RuleFor(x => x.Dto.DueDate).Must(d => d == null || d.Value.Date >= DateTime.Today)
                .WithMessage("تاريخ التسليم لا يمكن أن يكون في الماضي");
        }
    }

    public class UpdateAssignedTaskCommandValidator : AbstractValidator<UpdateAssignedTaskCommand>
    {
        public UpdateAssignedTaskCommandValidator()
        {
            RuleFor(x => x.Dto.Title).NotEmpty().WithMessage("عنوان المهمة مطلوب")
                .MaximumLength(200).WithMessage("العنوان لا يتجاوز 200 حرف");
            RuleFor(x => x.Dto.Description).MaximumLength(4000).WithMessage("الوصف لا يتجاوز 4000 حرف");
            RuleFor(x => x.Dto.Priority).InclusiveBetween(1, 4).WithMessage("الأولوية غير صحيحة");
        }
    }

    public class ChangeAssignedTaskStatusCommandValidator : AbstractValidator<ChangeAssignedTaskStatusCommand>
    {
        public ChangeAssignedTaskStatusCommandValidator()
        {
            RuleFor(x => x.Status).Must(s => Enum.IsDefined(typeof(AssignedTaskStatus), s)).WithMessage("الحالة غير صحيحة");
            RuleFor(x => x.Note).MaximumLength(500).WithMessage("السبب لا يتجاوز 500 حرف");
            RuleFor(x => x.ExpectedStatus!.Value).Must(s => Enum.IsDefined(typeof(AssignedTaskStatus), s))
                .When(x => x.ExpectedStatus != null).WithMessage("الحالة غير صحيحة");
        }
    }

    public class AddAssignedTaskCommentCommandValidator : AbstractValidator<AddAssignedTaskCommentCommand>
    {
        public AddAssignedTaskCommentCommandValidator()
        {
            RuleFor(x => x.Text).NotEmpty().WithMessage("اكتب نص التعليق")
                .MaximumLength(2000).WithMessage("التعليق لا يتجاوز 2000 حرف");
        }
    }

    // ════════════════════ المعالجات ════════════════════

    public class AssignedTaskCommandsHandler :
        IRequestHandler<CreateAssignedTaskCommand, AssignedTaskDetailDto>,
        IRequestHandler<UpdateAssignedTaskCommand, AssignedTaskDetailDto>,
        IRequestHandler<DeleteAssignedTaskCommand, Unit>,
        IRequestHandler<ChangeAssignedTaskStatusCommand, AssignedTaskCardDto>,
        IRequestHandler<AddAssignedTaskCommentCommand, AssignedTaskDetailDto>
    {
        private readonly IAssignedTaskService _taskService;
        private readonly IUserService _userService;
        private readonly AssignedTaskCreator _creator;
        private readonly IUserPermissionService _permissions;
        private readonly INotificationService _notificationService;

        public AssignedTaskCommandsHandler(
            IAssignedTaskService taskService,
            IUserService userService,
            IUserPermissionService permissions,
            AssignedTaskCreator creator,
            INotificationService notificationService)
        {
            _taskService = taskService;
            _userService = userService;
            _permissions = permissions;
            _creator = creator;
            _notificationService = notificationService;
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_userService, _permissions);

        private async Task<AssignedTask> LoadAsync(int id) =>
            await _taskService.GetByIdAsync(id) ?? throw new KeyNotFoundException("المهمة غير موجودة");

        // ─────────── إنشاء / تفويض (المنطق كله في AssignedTaskCreator) ───────────
        public async Task<AssignedTaskDetailDto> Handle(CreateAssignedTaskCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var dto = request.Dto;
            var created = await _creator.CreateAsync(viewer, new NewTaskSpec(
                dto.Title, dto.Description, dto.Priority, dto.DueDate, dto.TargetType, dto.TargetId, dto.ParentTaskId, dto.ChecklistItems));
            return AssignedTaskRules.ToDetail(created, viewer);
        }

        // ─────────── تعديل (المُسنِد فقط، قبل الإنجاز) ───────────
        public async Task<AssignedTaskDetailDto> Handle(UpdateAssignedTaskCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var user = viewer.User;
            var task = await LoadAsync(request.Id);

            if (task.CreatedByUserId != user.Id)
                throw new UnauthorizedAccessException("يعدّل المهمة من أسندها فقط");
            if (task.Status == AssignedTaskStatus.Done)
                throw new InvalidOperationException("لا يمكن تعديل مهمة منجزة");

            var dto = request.Dto;
            task.Title = dto.Title.Trim();
            task.Description = dto.Description.Trim();
            task.Priority = (AssignedTaskPriority)dto.Priority;
            // موعد جديد = تذكيرات جديدة (تُرسل مرة واحدة لكل موعد)
            if (task.DueDate?.Date != dto.DueDate?.Date) { task.DueSoonNotifiedAt = null; task.OverdueNotifiedAt = null; }
            task.DueDate = dto.DueDate?.Date;
            task.UpdatedAt = DateTime.UtcNow;
            await _taskService.SaveChangesAsync();
            await _taskService.AddActivityAsync(AssignedTaskRules.Activity(task, user, AssignedTaskActivityType.Edited, "عدّل تفاصيل المهمة"));

            return AssignedTaskRules.ToDetail(await LoadAsync(task.Id), viewer);
        }

        // ─────────── حذف (المُسنِد فقط، قبل البدء وبدون مهام فرعية) ───────────
        public async Task<Unit> Handle(DeleteAssignedTaskCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var user = viewer.User;
            var task = await LoadAsync(request.Id);

            if (task.CreatedByUserId != user.Id)
                throw new UnauthorizedAccessException("يحذف المهمة من أسندها فقط");
            if (task.Status != AssignedTaskStatus.Todo)
                throw new InvalidOperationException("لا يمكن حذف مهمة بدأ تنفيذها");
            if (task.SubTasks.Count > 0)
                throw new InvalidOperationException("لا يمكن حذف مهمة لها مهام فرعية");

            await _taskService.DeleteAsync(task);
            return Unit.Value;
        }

        // ─────────── تغيير الحالة (السحب والإفلات أو الأزرار) ───────────
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

        // ─────────── تعليق (كل من يطّلع على المهمة) ───────────
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
    }
}
