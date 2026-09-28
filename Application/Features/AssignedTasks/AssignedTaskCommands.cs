using Application.DTOs.Request;
using Application.DTOs.Response;
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
    public record ChangeAssignedTaskStatusCommand(int Id, int Status) : IRequest<AssignedTaskCardDto>;
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
            RuleFor(x => x.Status).InclusiveBetween(1, 3).WithMessage("الحالة غير صحيحة");
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
        private readonly IDepartmentService _departmentService;
        private readonly IOfficeService _officeService;
        private readonly INotificationService _notificationService;

        public AssignedTaskCommandsHandler(
            IAssignedTaskService taskService,
            IUserService userService,
            IDepartmentService departmentService,
            IOfficeService officeService,
            INotificationService notificationService)
        {
            _taskService = taskService;
            _userService = userService;
            _departmentService = departmentService;
            _officeService = officeService;
            _notificationService = notificationService;
        }

        private async Task<User> CurrentAsync() =>
            await _userService.GetByIdAsync(_userService.UserId)
            ?? throw new UnauthorizedAccessException("المستخدم غير مصادق");

        private async Task<AssignedTask> LoadAsync(int id) =>
            await _taskService.GetByIdAsync(id) ?? throw new KeyNotFoundException("المهمة غير موجودة");

        // ─────────── إنشاء / تفويض ───────────
        public async Task<AssignedTaskDetailDto> Handle(CreateAssignedTaskCommand request, CancellationToken ct)
        {
            var user = await CurrentAsync();
            var dto = request.Dto;
            var targetType = AssignedTaskRules.TargetTypeFor(user.Role?.Name ?? "")
                ?? throw new UnauthorizedAccessException("لا تملك صلاحية إسناد المهام");

            var now = DateTime.UtcNow;
            var task = new AssignedTask
            {
                Title = dto.Title.Trim(),
                Description = dto.Description.Trim(),
                Priority = (AssignedTaskPriority)dto.Priority,
                Status = AssignedTaskStatus.Todo,
                DueDate = dto.DueDate?.Date,
                TargetType = targetType,
                CreatedByUserId = user.Id,
                CreatedAt = now,
                UpdatedAt = now
            };

            // الجهة يجب أن تكون ضمن نطاق المُسنِد مباشرة (نزولاً درجة واحدة)
            switch (targetType)
            {
                case AssignedTaskTargetType.Department:
                    var department = await _departmentService.GetByIdAsync(dto.TargetId)
                        ?? throw new KeyNotFoundException("القسم غير موجود");
                    if (department.BranchId != user.BranchId)
                        throw new UnauthorizedAccessException("يمكنك إسناد المهام لأقسام فرعك فقط");
                    task.BranchId = department.BranchId;
                    task.DepartmentId = department.Id;
                    break;
                case AssignedTaskTargetType.Office:
                    var office = await _officeService.GetByIdAsync(dto.TargetId)
                        ?? throw new KeyNotFoundException("المكتب غير موجود");
                    if (office.DepartmentId != user.DepartmentId)
                        throw new UnauthorizedAccessException("يمكنك إسناد المهام لمكاتب قسمك فقط");
                    task.BranchId = office.Department?.BranchId ?? user.BranchId ?? 0;
                    task.DepartmentId = office.DepartmentId;
                    task.OfficeId = office.Id;
                    break;
                case AssignedTaskTargetType.User:
                    var assignee = await _userService.GetByIdAsync(dto.TargetId)
                        ?? throw new KeyNotFoundException("الموظف غير موجود");
                    if (assignee.OfficeId == null || assignee.OfficeId != user.OfficeId || assignee.Id == user.Id || !assignee.IsActive)
                        throw new UnauthorizedAccessException("يمكنك إسناد المهام لموظفي مكتبك فقط");
                    task.BranchId = assignee.BranchId ?? 0;
                    task.DepartmentId = assignee.DepartmentId;
                    task.OfficeId = assignee.OfficeId;
                    task.AssigneeUserId = assignee.Id;
                    break;
            }

            // التفويض: المهمة الأصل يجب أن تكون واردة إليّ (أنا منفِّذها) ولم تُنجز بعد
            AssignedTask? parent = null;
            if (dto.ParentTaskId is int parentId)
            {
                parent = await LoadAsync(parentId);
                if (!AssignedTaskRules.CanHandle(parent, user))
                    throw new UnauthorizedAccessException("يمكنك تفويض المهام الواردة إليك فقط");
                if (parent.Status == AssignedTaskStatus.Done)
                    throw new InvalidOperationException("لا يمكن التفويض من مهمة منجزة");
                task.ParentTaskId = parent.Id;
            }

            await _taskService.AddAsync(task);
            await _taskService.AddActivityAsync(AssignedTaskRules.Activity(task, user, AssignedTaskActivityType.Created, "أنشأ المهمة"));

            if (parent != null)
            {
                await _taskService.AddActivityAsync(AssignedTaskRules.Activity(parent, user, AssignedTaskActivityType.Delegated,
                    $"فوّض مهمة فرعية «{task.Title}»"));

                // بدء العمل على الأصل تلقائياً عند أول تفويض
                if (parent.Status == AssignedTaskStatus.Todo)
                {
                    parent.Status = AssignedTaskStatus.InProgress;
                    parent.StartedAt ??= now;
                    parent.UpdatedAt = now;
                    await _taskService.SaveChangesAsync();
                    await _taskService.AddActivityAsync(AssignedTaskRules.Activity(parent, user, AssignedTaskActivityType.StatusChanged,
                        "بدأ التنفيذ (بالتفويض)", AssignedTaskStatus.Todo, AssignedTaskStatus.InProgress));
                }
            }

            var created = await LoadAsync(task.Id);
            await AssignedTaskRules.NotifyAsync(_notificationService,
                await AssignedTaskRules.HandlerUserIdsAsync(_userService, created), user.Id, created,
                NotificationType.TaskAssigned, "مهمة جديدة",
                $"أسند إليك {user.FullName} مهمة: {created.Title}");

            return AssignedTaskRules.ToDetail(created, user);
        }

        // ─────────── تعديل (المُسنِد فقط، قبل الإنجاز) ───────────
        public async Task<AssignedTaskDetailDto> Handle(UpdateAssignedTaskCommand request, CancellationToken ct)
        {
            var user = await CurrentAsync();
            var task = await LoadAsync(request.Id);

            if (task.CreatedByUserId != user.Id)
                throw new UnauthorizedAccessException("يعدّل المهمة من أسندها فقط");
            if (task.Status == AssignedTaskStatus.Done)
                throw new InvalidOperationException("لا يمكن تعديل مهمة منجزة");

            var dto = request.Dto;
            task.Title = dto.Title.Trim();
            task.Description = dto.Description.Trim();
            task.Priority = (AssignedTaskPriority)dto.Priority;
            task.DueDate = dto.DueDate?.Date;
            task.UpdatedAt = DateTime.UtcNow;
            await _taskService.SaveChangesAsync();
            await _taskService.AddActivityAsync(AssignedTaskRules.Activity(task, user, AssignedTaskActivityType.Edited, "عدّل تفاصيل المهمة"));

            return AssignedTaskRules.ToDetail(await LoadAsync(task.Id), user);
        }

        // ─────────── حذف (المُسنِد فقط، قبل البدء وبدون مهام فرعية) ───────────
        public async Task<Unit> Handle(DeleteAssignedTaskCommand request, CancellationToken ct)
        {
            var user = await CurrentAsync();
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

        // ─────────── تغيير الحالة (السحب والإفلات — المنفِّذ فقط) ───────────
        public async Task<AssignedTaskCardDto> Handle(ChangeAssignedTaskStatusCommand request, CancellationToken ct)
        {
            var user = await CurrentAsync();
            var task = await LoadAsync(request.Id);

            if (!AssignedTaskRules.CanHandle(task, user))
                throw new UnauthorizedAccessException("تغيير حالة المهمة للجهة المنفِّذة فقط");

            var from = task.Status;
            var to = (AssignedTaskStatus)request.Status;
            if (from == to) return AssignedTaskRules.ToCard(task, user);

            var now = DateTime.UtcNow;
            task.Status = to;
            task.UpdatedAt = now;
            if (to == AssignedTaskStatus.InProgress) task.StartedAt ??= now;
            task.CompletedAt = to == AssignedTaskStatus.Done ? now : null;
            await _taskService.SaveChangesAsync();

            await _taskService.AddActivityAsync(AssignedTaskRules.Activity(task, user, AssignedTaskActivityType.StatusChanged,
                $"نقل المهمة من «{AssignedTaskRules.StatusAr(from)}» إلى «{AssignedTaskRules.StatusAr(to)}»", from, to));

            await AssignedTaskRules.NotifyAsync(_notificationService, [task.CreatedByUserId], user.Id, task,
                NotificationType.TaskStatusChanged,
                to == AssignedTaskStatus.Done ? "تم تنفيذ مهمة" : "تحديث على مهمة",
                $"«{task.Title}»: {AssignedTaskRules.StatusAr(to)} — بواسطة {user.FullName}");

            return AssignedTaskRules.ToCard(await LoadAsync(task.Id), user);
        }

        // ─────────── تعليق (كل من يطّلع على المهمة) ───────────
        public async Task<AssignedTaskDetailDto> Handle(AddAssignedTaskCommentCommand request, CancellationToken ct)
        {
            var user = await CurrentAsync();
            var task = await LoadAsync(request.Id);

            if (!AssignedTaskRules.CanView(task, user))
                throw new UnauthorizedAccessException("لا تملك صلاحية التعليق على هذه المهمة");

            var text = request.Text.Trim();
            await _taskService.AddActivityAsync(AssignedTaskRules.Activity(task, user, AssignedTaskActivityType.Comment, text));

            // المُسنِد + المنفِّذون (عدا كاتب التعليق)
            var recipients = (await AssignedTaskRules.HandlerUserIdsAsync(_userService, task)).Append(task.CreatedByUserId);
            await AssignedTaskRules.NotifyAsync(_notificationService, recipients, user.Id, task,
                NotificationType.TaskCommented, "تعليق جديد على مهمة",
                $"{user.FullName} على «{task.Title}»: {(text.Length > 80 ? text[..80] + "…" : text)}");

            return AssignedTaskRules.ToDetail(await LoadAsync(task.Id), user);
        }
    }
}
