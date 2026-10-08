using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.AssignedTasks
{
    /// <summary>طلب إنشاء مهمة (من النموذج، أو من قالب، أو من العامل الدوري)</summary>
    public sealed record NewTaskSpec(
        string Title, string Description, int Priority, DateTime? DueDate,
        string? TargetType, int TargetId, int? ParentTaskId,
        IReadOnlyList<string>? Checklist = null,
        /// <summary>غير null = مهمة دورية: يُكتب في السجل والإشعار بدل «أنشأ المهمة»</summary>
        string? RecurringName = null);

    /// <summary>
    /// المصدر الوحيد لإنشاء مهمة مُسندة: يتحقق من صلاحية المُسنِد وحدّ الجهة (أقسام فرعه / مكاتب قسمه / موظفو مكتبه، أو ضمن وحدة الأصل عند التفويض)
    /// ثم يحفظ المهمة وسجلها ويُشعر المنفِّذين. يستعمله معالج الإنشاء ومعالج المهام الدورية بـ Viewer صاحب التكرار وقت التنفيذ.
    /// </summary>
    public class AssignedTaskCreator
    {
        private readonly IAssignedTaskService _taskService;
        private readonly IUserService _userService;
        private readonly IDepartmentService _departmentService;
        private readonly IOfficeService _officeService;
        private readonly IUserPermissionService _permissions;
        private readonly INotificationService _notificationService;

        public AssignedTaskCreator(
            IAssignedTaskService taskService, IUserService userService, IDepartmentService departmentService,
            IOfficeService officeService, IUserPermissionService permissions, INotificationService notificationService)
        {
            _taskService = taskService;
            _userService = userService;
            _departmentService = departmentService;
            _officeService = officeService;
            _permissions = permissions;
            _notificationService = notificationService;
        }

        /// <summary>يتحقق أن المستخدم يستطيع الإسناد لهذه الجهة (دون إنشاء شيء) — لحفظ مهمة دورية</summary>
        public async Task EnsureTargetAllowedAsync(Viewer viewer, AssignedTaskTargetType type, int targetId)
        {
            var types = AssignedTaskRules.TargetTypesFor(viewer);
            if (!types.Contains(type)) throw new UnauthorizedAccessException("لا تملك صلاحية الإسناد لهذا النوع من الجهات");
            await ResolveTargetAsync(viewer, null, type, targetId, new AssignedTask { Title = "-" });
        }

        public async Task<AssignedTask> CreateAsync(Viewer viewer, NewTaskSpec spec)
        {
            var user = viewer.User;

            // التفويض: المهمة الأصل يجب أن تكون واردة إليّ (أنا منفِّذها) ولم تُنجز بعد، والجهة أدنى منها بدرجة
            AssignedTask? parent = null;
            AssignedTaskTargetType targetType;
            if (spec.ParentTaskId is int parentId)
            {
                parent = await _taskService.GetByIdAsync(parentId) ?? throw new KeyNotFoundException("المهمة غير موجودة");
                if (!AssignedTaskRules.CanHandle(parent, viewer))
                    throw new UnauthorizedAccessException("يمكنك تفويض المهام الواردة إليك فقط");
                if (parent.Status == AssignedTaskStatus.Done)
                    throw new InvalidOperationException("لا يمكن التفويض من مهمة منجزة");
                if (parent.Status == AssignedTaskStatus.InReview)
                    throw new InvalidOperationException("لا يمكن التفويض من مهمة بانتظار المراجعة");
                targetType = AssignedTaskRules.DelegationTargetFor(parent)
                    ?? throw new InvalidOperationException("لا يمكن تفويض مهمة موجّهة لموظف");
            }
            else
            {
                var types = AssignedTaskRules.TargetTypesFor(viewer);
                if (types.Count == 0)
                    throw new UnauthorizedAccessException("لا تملك صلاحية إسناد المهام");
                if (string.IsNullOrWhiteSpace(spec.TargetType))
                    targetType = types.Count == 1 ? types[0] : throw new ArgumentException("اختر نوع الجهة المُسندة إليها المهمة");
                else if (!Enum.TryParse(spec.TargetType, true, out targetType) || !types.Contains(targetType))
                    throw new UnauthorizedAccessException("لا تملك صلاحية الإسناد لهذا النوع من الجهات");
            }

            var now = DateTime.UtcNow;
            var task = new AssignedTask
            {
                Title = spec.Title.Trim(),
                Description = spec.Description.Trim(),
                Priority = (AssignedTaskPriority)spec.Priority,
                Status = AssignedTaskStatus.Todo,
                DueDate = spec.DueDate?.Date,
                TargetType = targetType,
                CreatedByUserId = user.Id,
                CreatedAt = now,
                UpdatedAt = now
            };

            await ResolveTargetAsync(viewer, parent, targetType, spec.TargetId, task);
            task.ParentTaskId = parent?.Id;

            var order = 0;
            foreach (var text in (spec.Checklist ?? []).Select(t => t.Trim()).Where(t => t.Length > 0).Take(AssignedTaskRules.MaxChecklistItems))
                task.ChecklistItems.Add(new AssignedTaskChecklistItem { Text = text, SortOrder = order++, CreatedAt = now });

            await _taskService.AddAsync(task);
            await _taskService.AddActivityAsync(AssignedTaskRules.Activity(task, user, AssignedTaskActivityType.Created,
                spec.RecurringName == null ? "أنشأ المهمة" : $"أُنشئت تلقائياً من المهمة الدورية «{spec.RecurringName}»"));

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

            var created = await _taskService.GetByIdAsync(task.Id) ?? throw new KeyNotFoundException("المهمة غير موجودة");
            await AssignedTaskRules.NotifyAsync(_notificationService,
                await AssignedTaskRules.HandlerUserIdsAsync(_permissions, created), user.Id, created,
                NotificationType.TaskAssigned, spec.RecurringName == null ? "مهمة جديدة" : "مهمة دورية جديدة",
                spec.RecurringName == null
                    ? $"أسند إليك {user.FullName} مهمة: {created.Title}"
                    : $"مهمة دورية جديدة من {user.FullName}: {created.Title}");
            return created;
        }

        /// <summary>الجهة ضمن حدّ الصلاحية (أقسام فرعي / مكاتب قسمي / موظفو مكتبي)، أو ضمن وحدة المهمة الأصل عند التفويض؛ ويملأ مسارها في المهمة</summary>
        private async Task ResolveTargetAsync(Viewer viewer, AssignedTask? parent, AssignedTaskTargetType targetType, int targetId, AssignedTask task)
        {
            var user = viewer.User;
            var branchLimit = parent?.BranchId ?? user.BranchId;
            var departmentLimit = parent != null ? parent.DepartmentId : user.DepartmentId;
            var officeLimit = parent != null ? parent.OfficeId : user.OfficeId;

            switch (targetType)
            {
                case AssignedTaskTargetType.Department:
                    var department = await _departmentService.GetByIdAsync(targetId)
                        ?? throw new KeyNotFoundException("القسم غير موجود");
                    if (department.BranchId != branchLimit)
                        throw new UnauthorizedAccessException("يمكنك إسناد المهام لأقسام فرعك فقط");
                    task.BranchId = department.BranchId;
                    task.DepartmentId = department.Id;
                    break;
                case AssignedTaskTargetType.Office:
                    var office = await _officeService.GetByIdAsync(targetId)
                        ?? throw new KeyNotFoundException("المكتب غير موجود");
                    if (departmentLimit == null || office.DepartmentId != departmentLimit)
                        throw new UnauthorizedAccessException(parent != null
                            ? "يمكنك التفويض لمكاتب قسم المهمة فقط" : "يمكنك إسناد المهام لمكاتب قسمك فقط");
                    task.BranchId = office.Department?.BranchId ?? branchLimit ?? 0;
                    task.DepartmentId = office.DepartmentId;
                    task.OfficeId = office.Id;
                    break;
                case AssignedTaskTargetType.User:
                    var assignee = await _userService.GetByIdAsync(targetId)
                        ?? throw new KeyNotFoundException("الموظف غير موجود");
                    if (officeLimit == null || assignee.OfficeId != officeLimit || assignee.Id == user.Id || !assignee.IsActive)
                        throw new UnauthorizedAccessException(parent != null
                            ? "يمكنك التفويض لموظفي مكتب المهمة فقط" : "يمكنك إسناد المهام لموظفي مكتبك فقط");
                    task.BranchId = assignee.BranchId ?? 0;
                    task.DepartmentId = assignee.DepartmentId;
                    task.OfficeId = assignee.OfficeId;
                    task.AssigneeUserId = assignee.Id;
                    break;
            }
        }
    }
}
