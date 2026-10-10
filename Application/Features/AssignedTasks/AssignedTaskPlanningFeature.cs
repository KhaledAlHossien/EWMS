using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.AssignedTasks
{
    // قوالب المهام والمهام الدورية والروابط (قرار المستخدم 2026-10-08). كلها لمن يملك صلاحية إسناد (AssignTaskTo*)،
    // والقالب والتكرار خاصان بصاحبهما، والرابط يحدده حق المستخدم في عرض السجل المرتبط (TaskLinkResolver).

    public record GetTaskTemplatesQuery : IRequest<List<TaskTemplateDto>>;
    public record SaveTaskTemplateCommand(int? Id, SaveTaskTemplateRequestDto Dto) : IRequest<TaskTemplateDto>;
    public record DeleteTaskTemplateCommand(int Id) : IRequest<Unit>;

    public record GetTaskRecurrencesQuery : IRequest<List<TaskRecurrenceDto>>;
    public record SaveTaskRecurrenceCommand(int? Id, SaveTaskRecurrenceRequestDto Dto) : IRequest<TaskRecurrenceDto>;
    public record SetTaskRecurrenceActiveCommand(int Id, bool IsActive) : IRequest<TaskRecurrenceDto>;
    public record DeleteTaskRecurrenceCommand(int Id) : IRequest<Unit>;
    /// <summary>ينشئ مهمة من التكرار الآن (دفعة إضافية)، ولا يغيّر موعده التالي</summary>
    public record RunTaskRecurrenceNowCommand(int Id) : IRequest<AssignedTaskDetailDto>;

    public record GetTaskLinksQuery(int TaskId) : IRequest<List<TaskLinkDto>>;
    public record AddTaskLinkCommand(int TaskId, AddTaskLinkRequestDto Dto) : IRequest<List<TaskLinkDto>>;
    public record RemoveTaskLinkCommand(int LinkId) : IRequest<List<TaskLinkDto>>;
    public record GetTasksByLinkQuery(string EntityType, int EntityId) : IRequest<List<AssignedTaskCardDto>>;

    public record GetTaskStatsQuery(DateTime? From, DateTime? To) : IRequest<TaskStatsDto>;

    // ════════════════════ التحقق ════════════════════

    public class SaveTaskTemplateCommandValidator : AbstractValidator<SaveTaskTemplateCommand>
    {
        public SaveTaskTemplateCommandValidator()
        {
            RuleFor(x => x.Dto.Name).NotEmpty().WithMessage("اسم القالب مطلوب").MaximumLength(100).WithMessage("اسم القالب لا يتجاوز 100 حرف");
            RuleFor(x => x.Dto.Title).NotEmpty().WithMessage("عنوان المهمة مطلوب").MaximumLength(200).WithMessage("العنوان لا يتجاوز 200 حرف");
            RuleFor(x => x.Dto.Description).MaximumLength(4000).WithMessage("الوصف لا يتجاوز 4000 حرف");
            RuleFor(x => x.Dto.Priority).InclusiveBetween(1, 4).WithMessage("الأولوية غير صحيحة");
            RuleFor(x => x.Dto.DefaultDueDays).Must(d => d == null || (d >= 0 && d <= 365)).WithMessage("مدة التسليم بين 0 و365 يوماً");
            RuleFor(x => x.Dto.Items).Must(l => l.Count <= AssignedTaskRules.MaxChecklistItems)
                .WithMessage($"بنود التحقق لا تتجاوز {AssignedTaskRules.MaxChecklistItems} بنداً");
            RuleForEach(x => x.Dto.Items).MaximumLength(200).WithMessage("البند لا يتجاوز 200 حرف");
        }
    }

    public class SaveTaskRecurrenceCommandValidator : AbstractValidator<SaveTaskRecurrenceCommand>
    {
        public SaveTaskRecurrenceCommandValidator()
        {
            RuleFor(x => x.Dto.TemplateId).GreaterThan(0).WithMessage("اختر القالب");
            RuleFor(x => x.Dto.TargetId).GreaterThan(0).WithMessage("اختر الجهة المُسندة إليها المهمة");
            RuleFor(x => x.Dto.Frequency).InclusiveBetween(1, 3).WithMessage("نوع التكرار غير صحيح");
            RuleFor(x => x.Dto.DayOfWeek).NotNull().When(x => x.Dto.Frequency == 2).WithMessage("اختر يوم الأسبوع")
                .InclusiveBetween(0, 6).When(x => x.Dto.DayOfWeek != null).WithMessage("يوم الأسبوع غير صحيح");
            RuleFor(x => x.Dto.DayOfMonth).NotNull().When(x => x.Dto.Frequency == 3).WithMessage("اختر يوم الشهر")
                .InclusiveBetween(1, 31).When(x => x.Dto.DayOfMonth != null).WithMessage("يوم الشهر بين 1 و31");
            RuleFor(x => x.Dto.DueAfterDays).Must(d => d == null || (d >= 0 && d <= 365)).WithMessage("مدة التسليم بين 0 و365 يوماً");
            RuleFor(x => x.Dto.EndDate).Must((x, end) => end == null || end.Value.Date >= x.Dto.StartDate.Date)
                .WithMessage("تاريخ الانتهاء قبل تاريخ البدء");
        }
    }

    public class AddTaskLinkCommandValidator : AbstractValidator<AddTaskLinkCommand>
    {
        public AddTaskLinkCommandValidator()
        {
            RuleFor(x => x.Dto.EntityType).NotEmpty().WithMessage("اختر نوع السجل");
            RuleFor(x => x.Dto).Must(d => d.EntityId > 0 || !string.IsNullOrWhiteSpace(d.Reference)).WithMessage("أدخل رقم السجل");
            RuleFor(x => x.Dto.Reference).MaximumLength(40).WithMessage("رقم السجل طويل");
        }
    }

    // ════════════════════ القوالب ════════════════════

    public class TaskTemplatesHandler :
        IRequestHandler<GetTaskTemplatesQuery, List<TaskTemplateDto>>,
        IRequestHandler<SaveTaskTemplateCommand, TaskTemplateDto>,
        IRequestHandler<DeleteTaskTemplateCommand, Unit>
    {
        private readonly IAssignedTaskPlanningService _planning;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public TaskTemplatesHandler(IAssignedTaskPlanningService planning, IUserService userService, IUserPermissionService permissions)
        {
            _planning = planning;
            _userService = userService;
            _permissions = permissions;
        }

        private async Task<Viewer> AssignerAsync()
        {
            var viewer = await Viewer.CurrentAsync(_userService, _permissions);
            if (AssignedTaskRules.TargetTypesFor(viewer).Count == 0)
                throw new UnauthorizedAccessException("القوالب والمهام الدورية لمن يملك صلاحية إسناد المهام");
            return viewer;
        }

        public async Task<List<TaskTemplateDto>> Handle(GetTaskTemplatesQuery request, CancellationToken ct)
        {
            var viewer = await AssignerAsync();
            return (await _planning.GetTemplatesAsync(viewer.Id)).Select(TaskPlanningRules.ToDto).ToList();
        }

        public async Task<TaskTemplateDto> Handle(SaveTaskTemplateCommand request, CancellationToken ct)
        {
            var viewer = await AssignerAsync();
            var dto = request.Dto;
            var name = dto.Name.Trim();
            var items = dto.Items.Select(i => i.Trim()).Where(i => i.Length > 0).ToList();

            if (await _planning.TemplateNameExistsAsync(viewer.Id, name, request.Id))
                throw new InvalidOperationException("لديك قالب بهذا الاسم");

            var now = DateTime.UtcNow;
            AssignedTaskTemplate template;
            if (request.Id is int id)
            {
                template = await _planning.GetTemplateAsync(id) ?? throw new KeyNotFoundException("القالب غير موجود");
                if (template.OwnerUserId != viewer.Id) throw new UnauthorizedAccessException("القالب لصاحبه فقط");
            }
            else
            {
                if ((await _planning.GetTemplatesAsync(viewer.Id)).Count >= TaskPlanningRules.MaxTemplates)
                    throw new InvalidOperationException($"الحد الأقصى {TaskPlanningRules.MaxTemplates} قالباً");
                template = new AssignedTaskTemplate { OwnerUserId = viewer.Id, CreatedAt = now };
            }

            template.Name = name;
            template.Title = dto.Title.Trim();
            template.Description = dto.Description.Trim();
            template.Priority = (AssignedTaskPriority)dto.Priority;
            template.DefaultDueDays = dto.DefaultDueDays;
            template.UpdatedAt = now;

            if (request.Id == null)
            {
                var order = 0;
                foreach (var text in items) template.Items.Add(new AssignedTaskTemplateItem { Text = text, SortOrder = order++ });
                await _planning.AddTemplateAsync(template);
            }
            else
            {
                await _planning.ReplaceTemplateItemsAsync(template, items);
            }
            return TaskPlanningRules.ToDto(template);
        }

        public async Task<Unit> Handle(DeleteTaskTemplateCommand request, CancellationToken ct)
        {
            var viewer = await AssignerAsync();
            var template = await _planning.GetTemplateAsync(request.Id) ?? throw new KeyNotFoundException("القالب غير موجود");
            if (template.OwnerUserId != viewer.Id) throw new UnauthorizedAccessException("القالب لصاحبه فقط");
            if (await _planning.TemplateInUseAsync(template.Id))
                throw new InvalidOperationException("القالب مستعمل في مهمة دورية — احذف المهمة الدورية أولاً");
            await _planning.DeleteTemplateAsync(template);
            return Unit.Value;
        }
    }

    // ════════════════════ المهام الدورية ════════════════════

    public class TaskRecurrencesHandler :
        IRequestHandler<GetTaskRecurrencesQuery, List<TaskRecurrenceDto>>,
        IRequestHandler<SaveTaskRecurrenceCommand, TaskRecurrenceDto>,
        IRequestHandler<SetTaskRecurrenceActiveCommand, TaskRecurrenceDto>,
        IRequestHandler<DeleteTaskRecurrenceCommand, Unit>,
        IRequestHandler<RunTaskRecurrenceNowCommand, AssignedTaskDetailDto>
    {
        private readonly IAssignedTaskPlanningService _planning;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly IDepartmentService _departments;
        private readonly IOfficeService _offices;
        private readonly AssignedTaskCreator _creator;

        public TaskRecurrencesHandler(
            IAssignedTaskPlanningService planning, IUserService userService, IUserPermissionService permissions,
            IDepartmentService departments, IOfficeService offices, AssignedTaskCreator creator)
        {
            _planning = planning;
            _userService = userService;
            _permissions = permissions;
            _departments = departments;
            _offices = offices;
            _creator = creator;
        }

        private async Task<Viewer> AssignerAsync()
        {
            var viewer = await Viewer.CurrentAsync(_userService, _permissions);
            if (AssignedTaskRules.TargetTypesFor(viewer).Count == 0)
                throw new UnauthorizedAccessException("القوالب والمهام الدورية لمن يملك صلاحية إسناد المهام");
            return viewer;
        }

        private async Task<string> TargetNameAsync(AssignedTaskTargetType type, int id) => type switch
        {
            AssignedTaskTargetType.Department => (await _departments.GetByIdAsync(id))?.Name ?? "(قسم محذوف)",
            AssignedTaskTargetType.Office => (await _offices.GetByIdAsync(id))?.Name ?? "(مكتب محذوف)",
            _ => (await _userService.GetByIdAsync(id))?.FullName ?? "(موظف محذوف)"
        };

        private async Task<AssignedTaskRecurrence> OwnedAsync(int id, Viewer viewer)
        {
            var r = await _planning.GetRecurrenceAsync(id) ?? throw new KeyNotFoundException("المهمة الدورية غير موجودة");
            if (r.OwnerUserId != viewer.Id) throw new UnauthorizedAccessException("المهمة الدورية لصاحبها فقط");
            return r;
        }

        public async Task<List<TaskRecurrenceDto>> Handle(GetTaskRecurrencesQuery request, CancellationToken ct)
        {
            var viewer = await AssignerAsync();
            var list = new List<TaskRecurrenceDto>();
            foreach (var r in await _planning.GetRecurrencesAsync(viewer.Id))
                list.Add(TaskPlanningRules.ToDto(r, await TargetNameAsync(r.TargetType, r.TargetId)));
            return list;
        }

        public async Task<TaskRecurrenceDto> Handle(SaveTaskRecurrenceCommand request, CancellationToken ct)
        {
            var viewer = await AssignerAsync();
            var dto = request.Dto;
            var template = await _planning.GetTemplateAsync(dto.TemplateId) ?? throw new KeyNotFoundException("القالب غير موجود");
            if (template.OwnerUserId != viewer.Id) throw new UnauthorizedAccessException("استعمل قالباً من قوالبك");
            if (!Enum.TryParse<AssignedTaskTargetType>(dto.TargetType, true, out var targetType))
                throw new ArgumentException("نوع الجهة غير صحيح");
            await _creator.EnsureTargetAllowedAsync(viewer, targetType, dto.TargetId);

            var now = DateTime.UtcNow;
            AssignedTaskRecurrence r;
            if (request.Id is int id) r = await OwnedAsync(id, viewer);
            else
            {
                if ((await _planning.GetRecurrencesAsync(viewer.Id)).Count >= TaskPlanningRules.MaxRecurrences)
                    throw new InvalidOperationException($"الحد الأقصى {TaskPlanningRules.MaxRecurrences} مهمة دورية");
                r = new AssignedTaskRecurrence { OwnerUserId = viewer.Id, CreatedAt = now };
            }

            r.TemplateId = template.Id;
            r.Template = template;
            r.TargetType = targetType;
            r.TargetId = dto.TargetId;
            r.Frequency = (TaskRecurrenceFrequency)dto.Frequency;
            r.DayOfWeek = r.Frequency == TaskRecurrenceFrequency.Weekly ? dto.DayOfWeek : null;
            r.DayOfMonth = r.Frequency == TaskRecurrenceFrequency.Monthly ? dto.DayOfMonth : null;
            r.DueAfterDays = dto.DueAfterDays;
            r.StartDate = dto.StartDate.Date;
            r.EndDate = dto.EndDate?.Date;
            r.LastError = null;

            var next = TaskPlanningRules.NextRun(r.Frequency, r.DayOfWeek, r.DayOfMonth, new[] { r.StartDate, DateTime.Today }.Max());
            if (r.EndDate != null && next > r.EndDate) throw new InvalidOperationException("المدة المحددة لا تتضمن أي موعد تنفيذ");
            r.NextRunDate = next;
            r.IsActive = true;

            if (request.Id == null) await _planning.AddRecurrenceAsync(r);
            else await _planning.SaveChangesAsync();
            return TaskPlanningRules.ToDto(r, await TargetNameAsync(r.TargetType, r.TargetId));
        }

        public async Task<TaskRecurrenceDto> Handle(SetTaskRecurrenceActiveCommand request, CancellationToken ct)
        {
            var viewer = await AssignerAsync();
            var r = await OwnedAsync(request.Id, viewer);
            if (request.IsActive)
            {
                await _creator.EnsureTargetAllowedAsync(viewer, r.TargetType, r.TargetId);
                var next = TaskPlanningRules.NextRun(r.Frequency, r.DayOfWeek, r.DayOfMonth, DateTime.Today);
                if (r.EndDate != null && next > r.EndDate) throw new InvalidOperationException("انتهت مدة هذه المهمة الدورية — عدّل تاريخ الانتهاء");
                r.NextRunDate = next;
                r.LastError = null;
            }
            r.IsActive = request.IsActive;
            await _planning.SaveChangesAsync();
            return TaskPlanningRules.ToDto(r, await TargetNameAsync(r.TargetType, r.TargetId));
        }

        public async Task<Unit> Handle(DeleteTaskRecurrenceCommand request, CancellationToken ct)
        {
            var viewer = await AssignerAsync();
            await _planning.DeleteRecurrenceAsync(await OwnedAsync(request.Id, viewer));
            return Unit.Value;
        }

        public async Task<AssignedTaskDetailDto> Handle(RunTaskRecurrenceNowCommand request, CancellationToken ct)
        {
            var viewer = await AssignerAsync();
            var r = await OwnedAsync(request.Id, viewer);
            var created = await _creator.CreateAsync(viewer, AssignedTaskRecurrenceRunner.SpecFor(r, DateTime.Today));
            r.LastRunAt = DateTime.UtcNow;
            r.LastTaskId = created.Id;
            r.LastError = null;
            await _planning.SaveChangesAsync();
            return AssignedTaskRules.ToDetail(created, viewer);
        }
    }

    // ════════════════════ الروابط ════════════════════

    public class TaskLinksHandler :
        IRequestHandler<GetTaskLinksQuery, List<TaskLinkDto>>,
        IRequestHandler<AddTaskLinkCommand, List<TaskLinkDto>>,
        IRequestHandler<RemoveTaskLinkCommand, List<TaskLinkDto>>,
        IRequestHandler<GetTasksByLinkQuery, List<AssignedTaskCardDto>>
    {
        private readonly IAssignedTaskService _tasks;
        private readonly IAssignedTaskPlanningService _planning;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly TaskLinkResolver _resolver;

        public TaskLinksHandler(IAssignedTaskService tasks, IAssignedTaskPlanningService planning,
            IUserService userService, IUserPermissionService permissions, TaskLinkResolver resolver)
        {
            _tasks = tasks;
            _planning = planning;
            _userService = userService;
            _permissions = permissions;
            _resolver = resolver;
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_userService, _permissions);

        private async Task<AssignedTask> ViewableTaskAsync(int taskId, Viewer viewer)
        {
            var task = await _tasks.GetByIdAsync(taskId) ?? throw new KeyNotFoundException("المهمة غير موجودة");
            if (!AssignedTaskRules.CanView(task, viewer)) throw new UnauthorizedAccessException("لا تملك صلاحية عرض هذه المهمة");
            return task;
        }

        private async Task<List<TaskLinkDto>> ListAsync(AssignedTask task, Viewer viewer)
        {
            var list = new List<TaskLinkDto>();
            foreach (var l in await _planning.GetLinksAsync(task.Id))
            {
                var d = await _resolver.DescribeAsync(viewer, l.EntityType, l.EntityId);
                list.Add(new TaskLinkDto
                {
                    Id = l.Id,
                    EntityType = l.EntityType.ToString(),
                    EntityTypeAr = TaskLinkResolver.TypeAr(l.EntityType),
                    EntityId = l.EntityId,
                    Available = d.Available,
                    Label = d.Available ? d.Label : "سجل غير متاح لك",
                    CanRemove = !task.Status.Equals(AssignedTaskStatus.Done) && !AssignedTaskRules.IsFrozenFor(task, viewer)
                        && (l.CreatedByUserId == viewer.Id || task.CreatedByUserId == viewer.Id || viewer.IsSuperAdmin),
                    CreatedByName = l.CreatedByUser?.FullName ?? string.Empty
                });
            }
            return list;
        }

        public async Task<List<TaskLinkDto>> Handle(GetTaskLinksQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            return await ListAsync(await ViewableTaskAsync(request.TaskId, viewer), viewer);
        }

        public async Task<List<TaskLinkDto>> Handle(AddTaskLinkCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var task = await ViewableTaskAsync(request.TaskId, viewer);
            if (task.CreatedByUserId != viewer.Id && !AssignedTaskRules.CanHandle(task, viewer) && !viewer.IsSuperAdmin)
                throw new UnauthorizedAccessException("الربط للمُسنِد أو الجهة المنفِّذة");
            if (task.Status == AssignedTaskStatus.Done) throw new InvalidOperationException("لا يمكن الربط بمهمة منجزة");
            AssignedTaskRules.EnsureNotFrozen(task, viewer);
            if (!TaskLinkResolver.TryParseType(request.Dto.EntityType, out var type)) throw new ArgumentException("نوع السجل غير صحيح");
            if (await _planning.CountLinksAsync(task.Id) >= TaskPlanningRules.MaxLinksPerTask)
                throw new InvalidOperationException($"الحد الأقصى {TaskPlanningRules.MaxLinksPerTask} روابط للمهمة");

            var entityId = request.Dto.EntityId is > 0 ? request.Dto.EntityId.Value
                : TaskLinkResolver.ParseReference(request.Dto.Reference) ?? throw new ArgumentException("رقم السجل غير مفهوم");
            var described = await _resolver.DescribeAsync(viewer, type, entityId);
            if (!described.Exists || !described.Available)
                throw new KeyNotFoundException("السجل غير موجود أو لا تملك صلاحية عرضه");
            if (await _planning.LinkExistsAsync(task.Id, type, entityId)) throw new InvalidOperationException("هذا السجل مرتبط بالمهمة بالفعل");

            await _planning.AddLinkAsync(new AssignedTaskLink
            {
                AssignedTaskId = task.Id, EntityType = type, EntityId = entityId,
                CreatedByUserId = viewer.Id, CreatedAt = DateTime.UtcNow
            });
            await _tasks.AddActivityAsync(AssignedTaskRules.Activity(task, viewer.User, AssignedTaskActivityType.Edited,
                $"ربط المهمة بـ{TaskLinkResolver.TypeAr(type)}: {described.Label}"));
            return await ListAsync(task, viewer);
        }

        public async Task<List<TaskLinkDto>> Handle(RemoveTaskLinkCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var link = await _planning.GetLinkAsync(request.LinkId) ?? throw new KeyNotFoundException("الرابط غير موجود");
            var task = await ViewableTaskAsync(link.AssignedTaskId, viewer);
            if (link.CreatedByUserId != viewer.Id && task.CreatedByUserId != viewer.Id && !viewer.IsSuperAdmin)
                throw new UnauthorizedAccessException("يحذف الرابط من أضافه أو من أسند المهمة");
            if (task.Status == AssignedTaskStatus.Done) throw new InvalidOperationException("لا يمكن تعديل روابط مهمة منجزة");
            AssignedTaskRules.EnsureNotFrozen(task, viewer);
            await _planning.DeleteLinkAsync(link);
            return await ListAsync(task, viewer);
        }

        public async Task<List<AssignedTaskCardDto>> Handle(GetTasksByLinkQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            if (!TaskLinkResolver.TryParseType(request.EntityType, out var type)) throw new ArgumentException("نوع السجل غير صحيح");
            // لا يرى المهام المرتبطة إلا من يحق له عرض السجل نفسه
            var described = await _resolver.DescribeAsync(viewer, type, request.EntityId);
            if (!described.Available) return [];

            var ids = await _planning.GetTaskIdsByLinkAsync(type, request.EntityId);
            if (ids.Count == 0) return [];
            return (await _tasks.GetBoardAsync(t => ids.Contains(t.Id)))
                .Where(t => AssignedTaskRules.CanView(t, viewer))
                .Select(t => AssignedTaskRules.ToCard(t, viewer)).ToList();
        }
    }

    // ════════════════════ الإحصائيات ════════════════════

    public class TaskStatsHandler : IRequestHandler<GetTaskStatsQuery, TaskStatsDto>
    {
        private readonly IAssignedTaskPlanningService _planning;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public TaskStatsHandler(IAssignedTaskPlanningService planning, IUserService userService, IUserPermissionService permissions)
        {
            _planning = planning;
            _userService = userService;
            _permissions = permissions;
        }

        public async Task<TaskStatsDto> Handle(GetTaskStatsQuery request, CancellationToken ct)
        {
            var viewer = await Viewer.CurrentAsync(_userService, _permissions);
            var today = DateTime.Today;
            var to = (request.To ?? today).Date.AddDays(1);                  // نهاية اليوم (حصرية)
            var from = (request.From ?? to.AddDays(-91)).Date;
            if (from >= to) throw new ArgumentException("تاريخ البداية بعد تاريخ النهاية");
            if ((to - from).TotalDays > 731) throw new ArgumentException("الفترة أطول من سنتين");

            // الحدّ: مهام نطاقي (AssignedTaskRules.Scope) التي أُنشئت ضمن الفترة
            var rows = await _planning.GetStatsRowsAsync(AssignedTaskRules.Scope(viewer), from.ToUniversalTime(), to.ToUniversalTime());

            static bool IsDone(TaskStatsRow r) => r.Status == AssignedTaskStatus.Done;
            bool IsOverdue(TaskStatsRow r) => !IsDone(r) && r.DueDate != null && r.DueDate.Value.Date < today;
            static DateTime Local(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc).ToLocalTime();
            static double? OnTime(IEnumerable<TaskStatsRow> rs)
            {
                var due = rs.Where(r => IsDone(r) && r.DueDate != null && r.CompletedAt != null).ToList();
                return due.Count == 0 ? null : Math.Round(100.0 * due.Count(r => Local(r.CompletedAt!.Value).Date <= r.DueDate!.Value.Date) / due.Count, 1);
            }
            static double? AvgDays(IEnumerable<TaskStatsRow> rs)
            {
                var done = rs.Where(r => IsDone(r) && r.CompletedAt != null).ToList();
                return done.Count == 0 ? null : Math.Round(done.Average(r => (r.CompletedAt!.Value - r.CreatedAt).TotalDays), 1);
            }

            var dto = new TaskStatsDto
            {
                From = from, To = to.AddDays(-1),
                Total = rows.Count,
                Done = rows.Count(IsDone),
                Open = rows.Count(r => !IsDone(r)),
                InReview = rows.Count(r => r.Status == AssignedTaskStatus.InReview),
                Overdue = rows.Count(IsOverdue),
                OnTimeRate = OnTime(rows),
                AvgDays = AvgDays(rows),
                ReturnedTasks = rows.Count(r => r.ReturnCount > 0),
                ByTarget = rows.GroupBy(r => (r.TargetType, r.TargetKey)).Select(g => new TaskStatsGroupDto
                {
                    TargetType = g.Key.TargetType.ToString(),
                    TargetTypeAr = AssignedTaskRules.TargetTypeLabel(g.Key.TargetType),
                    Name = g.First().TargetName,
                    Total = g.Count(),
                    Done = g.Count(IsDone),
                    Open = g.Count(r => !IsDone(r)),
                    Overdue = g.Count(IsOverdue),
                    OnTimeRate = OnTime(g),
                    AvgDays = AvgDays(g),
                    Returned = g.Sum(r => r.ReturnCount)
                }).OrderByDescending(g => g.Overdue).ThenByDescending(g => g.Total).ToList()
            };

            // شهرياً: ما أُنشئ في الشهر وما أُنجز فيه (من مهام الفترة)
            var months = new List<TaskStatsMonthDto>();
            for (var m = new DateTime(from.Year, from.Month, 1); m < to; m = m.AddMonths(1))
                months.Add(new TaskStatsMonthDto
                {
                    Month = m.ToString("yyyy-MM"),
                    Created = rows.Count(r => Local(r.CreatedAt).Year == m.Year && Local(r.CreatedAt).Month == m.Month),
                    Done = rows.Count(r => IsDone(r) && r.CompletedAt != null && Local(r.CompletedAt.Value).Year == m.Year && Local(r.CompletedAt.Value).Month == m.Month)
                });
            dto.ByMonth = months.TakeLast(12).ToList();
            return dto;
        }
    }
}
