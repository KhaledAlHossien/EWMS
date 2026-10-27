using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.Schedules
{
    // الجدول الزمني للمشروع: الصلاحية (في المتحكم) تفتح العملية، وحدّ الجدول هو حدّ رؤية مشروعه (ProjectRules.Visible)
    // يُفحص هنا لكل سجل. القواعد كلها في ScheduleRules. كل كتابة تحفظ أثرها في سجل التدقيق بمعاملتها.
    public record GetScheduleOverviewQuery(int ProjectId) : IRequest<ScheduleOverviewResponseDto>;
    public record SaveStartOrderCommand(int ProjectId, StartOrderRequestDto Dto) : IRequest<StartOrderResponseDto>;
    public record GetScheduleQuery(int Id) : IRequest<ScheduleDetailResponseDto>;
    public record CreateScheduleDraftCommand(int ProjectId, ScheduleDraftRequestDto Dto) : IRequest<ScheduleDetailResponseDto>;
    public record DeleteScheduleDraftCommand(int Id) : IRequest<Unit>;
    public record ApproveScheduleCommand(int Id) : IRequest<ScheduleDetailResponseDto>;
    public record AddScheduleTaskCommand(int ScheduleId, ScheduleTaskRequestDto Dto) : IRequest<ScheduleTaskResponseDto>;
    public record UpdateScheduleTaskCommand(int TaskId, ScheduleTaskRequestDto Dto) : IRequest<ScheduleTaskResponseDto>;
    public record DeleteScheduleTaskCommand(int TaskId) : IRequest<Unit>;
    public record ReorderScheduleTasksCommand(int ScheduleId, ReorderScheduleTasksRequestDto Dto) : IRequest<Unit>;
    public record AddScheduleDependencyCommand(int ScheduleId, ScheduleDependencyRequestDto Dto) : IRequest<ScheduleDependencyResponseDto>;
    public record RemoveScheduleDependencyCommand(int Id) : IRequest<Unit>;
    public record UpdateScheduleProgressCommand(int TaskId, UpdateScheduleProgressRequestDto Dto) : IRequest<ScheduleTaskResponseDto>;
    public record ClassifyScheduleTaskCommand(int TaskId, ClassifyScheduleTaskRequestDto Dto) : IRequest<ScheduleTaskResponseDto>;
    public record GetScheduleProgressHistoryQuery(int TaskId) : IRequest<List<ScheduleProgressEntryResponseDto>>;

    // ════════════════════ التحقق من المدخلات (الأطوال؛ القيم والتواريخ في ScheduleRules) ════════════════════

    public class SaveStartOrderCommandValidator : AbstractValidator<SaveStartOrderCommand>
    {
        public SaveStartOrderCommandValidator()
        {
            RuleFor(x => x.Dto.OrderNumber).NotEmpty().WithMessage("رقم أمر المباشرة مطلوب").MaximumLength(50).WithMessage("رقم الأمر لا يتجاوز 50 حرفاً");
            RuleFor(x => x.Dto.Notes).MaximumLength(2000).WithMessage("الملاحظات لا تتجاوز 2000 حرف");
        }
    }

    public class CreateScheduleDraftCommandValidator : AbstractValidator<CreateScheduleDraftCommand>
    {
        public CreateScheduleDraftCommandValidator()
        {
            RuleFor(x => x.Dto.Title).MaximumLength(200).WithMessage("عنوان الجدول لا يتجاوز 200 حرف");
            RuleFor(x => x.Dto.Note).MaximumLength(1000).WithMessage("الملاحظة لا تتجاوز 1000 حرف");
        }
    }

    public class ScheduleTaskRequestDtoValidator : AbstractValidator<ScheduleTaskRequestDto>
    {
        public ScheduleTaskRequestDtoValidator()
        {
            RuleFor(x => x.Code).MaximumLength(30).WithMessage("رمز البند لا يتجاوز 30 حرفاً");
            RuleFor(x => x.Note).MaximumLength(2000).WithMessage("ملاحظة البند لا تتجاوز 2000 حرف");
        }
    }

    public class AddScheduleTaskCommandValidator : AbstractValidator<AddScheduleTaskCommand>
    {
        public AddScheduleTaskCommandValidator() => RuleFor(x => x.Dto).SetValidator(new ScheduleTaskRequestDtoValidator());
    }

    public class UpdateScheduleTaskCommandValidator : AbstractValidator<UpdateScheduleTaskCommand>
    {
        public UpdateScheduleTaskCommandValidator() => RuleFor(x => x.Dto).SetValidator(new ScheduleTaskRequestDtoValidator());
    }

    public class UpdateScheduleProgressCommandValidator : AbstractValidator<UpdateScheduleProgressCommand>
    {
        public UpdateScheduleProgressCommandValidator()
        {
            RuleFor(x => x.Dto.Status).Must(s => Enum.IsDefined(typeof(ScheduleTaskStatus), s)).WithMessage("حالة البند غير صحيحة");
            RuleFor(x => x.Dto.Note).MaximumLength(1000).WithMessage("الملاحظة لا تتجاوز 1000 حرف");
        }
    }

    // ════════════════════ المعالج ════════════════════

    public class ScheduleHandler :
        IRequestHandler<GetScheduleOverviewQuery, ScheduleOverviewResponseDto>,
        IRequestHandler<SaveStartOrderCommand, StartOrderResponseDto>,
        IRequestHandler<GetScheduleQuery, ScheduleDetailResponseDto>,
        IRequestHandler<CreateScheduleDraftCommand, ScheduleDetailResponseDto>,
        IRequestHandler<DeleteScheduleDraftCommand, Unit>,
        IRequestHandler<ApproveScheduleCommand, ScheduleDetailResponseDto>,
        IRequestHandler<AddScheduleTaskCommand, ScheduleTaskResponseDto>,
        IRequestHandler<UpdateScheduleTaskCommand, ScheduleTaskResponseDto>,
        IRequestHandler<DeleteScheduleTaskCommand, Unit>,
        IRequestHandler<ReorderScheduleTasksCommand, Unit>,
        IRequestHandler<AddScheduleDependencyCommand, ScheduleDependencyResponseDto>,
        IRequestHandler<RemoveScheduleDependencyCommand, Unit>,
        IRequestHandler<UpdateScheduleProgressCommand, ScheduleTaskResponseDto>,
        IRequestHandler<ClassifyScheduleTaskCommand, ScheduleTaskResponseDto>,
        IRequestHandler<GetScheduleProgressHistoryQuery, List<ScheduleProgressEntryResponseDto>>
    {
        private readonly IScheduleService _schedules;
        private readonly IProjectService _projects;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;

        public ScheduleHandler(IScheduleService schedules, IProjectService projects, IUserService users, IUserPermissionService permissions)
        {
            _schedules = schedules;
            _projects = projects;
            _users = users;
            _permissions = permissions;
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_users, _permissions);

        private static DateTime Today => DateTime.Today;

        // ───────── التحميل بعد فحص الحدّ ─────────

        private async Task<Project> LoadProjectAsync(Viewer viewer, int projectId)
        {
            var project = await _projects.GetByIdAsync(projectId) ?? throw new KeyNotFoundException("المشروع غير موجود");
            ProjectRules.EnsureView(viewer, project);
            return project;
        }

        private async Task<ProjectSchedule> LoadScheduleAsync(Viewer viewer, int id)
        {
            var schedule = await _schedules.GetByIdAsync(id) ?? throw new KeyNotFoundException("الجدول غير موجود");
            ProjectRules.EnsureView(viewer, schedule.Project);
            return schedule;
        }

        private async Task<ScheduleTask> LoadTaskAsync(Viewer viewer, int id)
        {
            var task = await _schedules.GetTaskAsync(id) ?? throw new KeyNotFoundException("البند غير موجود");
            ProjectRules.EnsureView(viewer, task.Schedule.Project);
            return task;
        }

        /// <summary>البنود والتبعيات تُحرَّر في المسودة فقط، وفي مشروع مفتوح</summary>
        private static void EnsureDraftEditable(ProjectSchedule schedule)
        {
            ProjectRules.EnsureOpen(schedule.Project);
            if (schedule.Status != ScheduleStatus.Draft)
                throw new InvalidOperationException(schedule.Status == ScheduleStatus.Approved
                    ? "الجدول المعتمد لا تُعدَّل بنوده مباشرة: التواريخ تتغير بتمديد معتمد، ولإعادة الترتيب أنشئ مسودة جديدة"
                    : "هذا الإصدار سابق ومحفوظ للمراجعة فقط");
        }

        // ───────── القراءة ─────────

        public async Task<ScheduleOverviewResponseDto> Handle(GetScheduleOverviewQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var project = await LoadProjectAsync(viewer, request.ProjectId);
            var startOrder = await _schedules.GetStartOrderAsync(project.Id);
            var versions = await _schedules.GetVersionsAsync(project.Id);

            var dto = new ScheduleOverviewResponseDto
            {
                ProjectId = project.Id,
                ProjectNumber = ProjectRules.Number(project.Id, project.CreatedAt),
                ProjectName = project.Name,
                ProjectFinished = ProjectRules.IsFinished(project.Stage),
                StartOrder = startOrder == null ? null : ToStartOrderDto(startOrder),
                Versions = versions.OrderByDescending(v => v.Schedule.VersionNumber).Select(ToVersionDto).ToList(),
                ApprovedScheduleId = versions.FirstOrDefault(v => v.Schedule.Status == ScheduleStatus.Approved)?.Schedule.Id,
                DraftScheduleId = versions.FirstOrDefault(v => v.Schedule.Status == ScheduleStatus.Draft)?.Schedule.Id,
                CanEditStartOrder = viewer.Has(AppPermissions.EditProjectStartOrder) && !ProjectRules.IsFinished(project.Stage),
                CanEditSchedule = viewer.Has(AppPermissions.EditProjectSchedule) && !ProjectRules.IsFinished(project.Stage)
            };

            if (dto.ApprovedScheduleId is int approvedId)
            {
                var approved = await _schedules.GetByIdAsync(approvedId);
                if (approved != null)
                {
                    var today = Today;
                    dto.ActualProgress = ScheduleRules.ActualProgress(approved.Tasks);
                    dto.PlannedProgress = ScheduleRules.PlannedProgress(approved.Tasks, today);
                    dto.BaselineProgress = ScheduleRules.PlannedProgress(approved.Tasks, today, useOriginal: true);
                    dto.OverdueCandidates = approved.Tasks.Count(t => ScheduleRules.IsOverdueCandidate(t, today));
                }
            }
            return dto;
        }

        public async Task<ScheduleDetailResponseDto> Handle(GetScheduleQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            return await DetailAsync(viewer, await LoadScheduleAsync(viewer, request.Id));
        }

        public async Task<List<ScheduleProgressEntryResponseDto>> Handle(GetScheduleProgressHistoryQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            await LoadTaskAsync(viewer, request.TaskId);
            var entries = await _schedules.GetProgressHistoryAsync(request.TaskId, take: 100);
            return entries.Select(e => new ScheduleProgressEntryResponseDto
            {
                Id = e.Id, Progress = e.Progress, Status = (int)e.Status, StatusName = ScheduleRules.TaskStatusAr(e.Status),
                Note = e.Note, UserName = e.User?.FullName ?? string.Empty, CreatedAt = e.CreatedAt
            }).ToList();
        }

        // ───────── أمر المباشرة ─────────

        public async Task<StartOrderResponseDto> Handle(SaveStartOrderCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var project = await LoadProjectAsync(viewer, request.ProjectId);
            ProjectRules.EnsureOpen(project);
            var dto = request.Dto;

            if (ScheduleRules.ValidateStartOrder(dto.OrderNumber, dto.OrderDate, dto.StartDate, dto.CompletionDays, dto.ExecutionDays, dto.StudyCost) is string problem)
                throw new InvalidOperationException(problem);

            // مهندس الموقع ومشرف القطاع من مشاركي المشروع
            var engineer = MemberOrNull(project, dto.SiteEngineerUserId, "مهندس الموقع");
            var supervisor = MemberOrNull(project, dto.SectorSupervisorUserId, "مشرف القطاع");

            var existing = await _schedules.GetStartOrderAsync(project.Id);
            var isNew = existing == null;
            var order = existing ?? new ProjectStartOrder { ProjectId = project.Id, CreatedByUserId = viewer.Id, CreatedAt = DateTime.UtcNow };

            var changes = new List<string>();
            void Track(string label, string? from, string? to)
            {
                if (!string.Equals(from ?? string.Empty, to ?? string.Empty, StringComparison.Ordinal))
                    changes.Add($"{label}: {(string.IsNullOrEmpty(from) ? "—" : from)} ← {(string.IsNullOrEmpty(to) ? "—" : to)}");
            }

            var number = dto.OrderNumber.Trim();
            var notes = (dto.Notes ?? string.Empty).Trim();
            Track("رقم الأمر", order.OrderNumber, number);
            Track("تاريخ الأمر", isNew ? null : Format(order.OrderDate), Format(dto.OrderDate));
            Track("تاريخ المباشرة", isNew ? null : Format(order.StartDate), Format(dto.StartDate));
            Track("مدة الإنجاز (يوم)", isNew ? null : order.CompletionDays.ToString(), dto.CompletionDays.ToString());
            Track("زمن التنفيذ (يوم)", isNew ? null : order.ExecutionDays.ToString(), dto.ExecutionDays.ToString());
            Track("تكلفة الدراسة", order.StudyCost?.ToString("0.##"), dto.StudyCost?.ToString("0.##"));
            Track("مهندس الموقع", order.SiteEngineer?.FullName, engineer?.FullName);
            Track("مشرف القطاع", order.SectorSupervisor?.FullName, supervisor?.FullName);
            if (order.Notes != notes) changes.Add("الملاحظات: تغيّرت");

            if (!isNew && changes.Count == 0) return ToStartOrderDto(order);

            order.OrderNumber = number;
            order.OrderDate = dto.OrderDate.Date;
            order.StartDate = dto.StartDate.Date;
            order.CompletionDays = dto.CompletionDays;
            order.ExecutionDays = dto.ExecutionDays;
            order.StudyCost = dto.StudyCost;
            order.SiteEngineerUserId = engineer?.Id;
            order.SiteEngineer = engineer;
            order.SectorSupervisorUserId = supervisor?.Id;
            order.SectorSupervisor = supervisor;
            order.Notes = notes;

            await _schedules.SaveStartOrderAsync(order, isNew, dto.RowVersion, new AuditEntry(viewer.Id,
                isNew ? AuditActions.Created : AuditActions.Updated, AuditEntities.StartOrder, null,
                $"{(isNew ? "تسجيل" : "تعديل")} أمر المباشرة للمشروع «{project.Name}»", string.Join("\n", changes)), project.BranchId);

            return ToStartOrderDto(await _schedules.GetStartOrderAsync(project.Id) ?? order);
        }

        private static User? MemberOrNull(Project project, int? userId, string role)
        {
            if (userId is not int id) return null;
            return project.Members.FirstOrDefault(m => m.UserId == id)?.User
                ?? throw new InvalidOperationException($"{role} يُختار من مشاركي المشروع — أضفه مشاركاً أولاً");
        }

        // ───────── إصدارات الجدول ─────────

        public async Task<ScheduleDetailResponseDto> Handle(CreateScheduleDraftCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var project = await LoadProjectAsync(viewer, request.ProjectId);
            ProjectRules.EnsureOpen(project);

            var versions = await _schedules.GetVersionsAsync(project.Id);
            if (versions.Any(v => v.Schedule.Status == ScheduleStatus.Draft))
                throw new InvalidOperationException("توجد مسودة جدول بالفعل: أكملها واعتمدها أو احذفها قبل إنشاء غيرها");
            if (await _schedules.HasPendingExtensionAsync(project.Id))
                throw new InvalidOperationException("يوجد طلب تمديد لم يُبتّ فيه: اعتمده أو ارفضه قبل إنشاء إصدار جديد من الجدول");

            // إعادة الإصدار تنسخ بنود المعتمد بتواريخه الأصلية فلا يضيع خط الأساس
            var approved = await _schedules.GetApprovedAsync(project.Id);
            List<(int OldId, ScheduleTask Copy)> copies = approved == null ? new() : ScheduleRules.CloneForRevision(approved.Tasks);
            List<ScheduleRules.Edge> edges = approved == null ? new() : (await _schedules.GetDependenciesAsync(approved.Id))
                .Select(d => new ScheduleRules.Edge(d.PredecessorId, d.SuccessorId)).ToList();

            var draft = new ProjectSchedule
            {
                ProjectId = project.Id,
                Title = (request.Dto.Title ?? string.Empty).Trim(),
                Note = (request.Dto.Note ?? string.Empty).Trim(),
                Status = ScheduleStatus.Draft,
                CreatedByUserId = viewer.Id,
                CreatedAt = DateTime.UtcNow
            };

            var source = approved == null ? "جدول فارغ" : $"نسخة من الإصدار {approved.VersionNumber} ({copies.Count} بنداً)";
            var created = await _schedules.CreateDraftAsync(draft, copies, edges, new AuditEntry(viewer.Id, AuditActions.Created,
                AuditEntities.Schedule, null, $"إنشاء مسودة جدول للمشروع «{project.Name}»", source), project.BranchId);

            return await DetailAsync(viewer, await LoadScheduleAsync(viewer, created.Id));
        }

        public async Task<Unit> Handle(DeleteScheduleDraftCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var schedule = await LoadScheduleAsync(viewer, request.Id);
            EnsureDraftEditable(schedule);

            await _schedules.DeleteDraftAsync(schedule, new AuditEntry(viewer.Id, AuditActions.Deleted, AuditEntities.Schedule, schedule.Id,
                $"حذف مسودة الجدول {schedule.VersionNumber} من «{schedule.Project.Name}»", $"البنود: {schedule.Tasks.Count}"), schedule.Project.BranchId);
            return Unit.Value;
        }

        public async Task<ScheduleDetailResponseDto> Handle(ApproveScheduleCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var schedule = await LoadScheduleAsync(viewer, request.Id);
            ProjectRules.EnsureOpen(schedule.Project);
            ScheduleRules.EnsureCanApprove(viewer, schedule);

            var dependencies = await _schedules.GetDependenciesAsync(schedule.Id);
            var edges = dependencies.Select(d => new ScheduleRules.Edge(d.PredecessorId, d.SuccessorId)).ToList();
            if (ScheduleRules.ValidateForApproval(schedule.Tasks.ToList(), edges) is string problem)
                throw new InvalidOperationException(problem);

            var previous = await _schedules.GetApprovedAsync(schedule.ProjectId);
            var details = $"البنود: {schedule.Tasks.Count}\nالتبعيات: {dependencies.Count}"
                          + (previous != null ? $"\nحلّ محل الإصدار {previous.VersionNumber}" : "\nأول إصدار معتمد: ثُبّتت التواريخ الأصلية");

            await _schedules.ApproveAsync(schedule, previous, viewer.Id, new AuditEntry(viewer.Id, AuditActions.Approved, AuditEntities.Schedule,
                schedule.Id, $"اعتماد الجدول {schedule.VersionNumber} للمشروع «{schedule.Project.Name}»", details), schedule.Project.BranchId);

            return await DetailAsync(viewer, await LoadScheduleAsync(viewer, schedule.Id));
        }

        // ───────── البنود ─────────

        public async Task<ScheduleTaskResponseDto> Handle(AddScheduleTaskCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var schedule = await LoadScheduleAsync(viewer, request.ScheduleId);
            EnsureDraftEditable(schedule);
            var dto = request.Dto;

            if (schedule.Tasks.Count >= ScheduleRules.MaxTasks)
                throw new InvalidOperationException($"بلغ الجدول الحد الأقصى للبنود ({ScheduleRules.MaxTasks})");
            if (ScheduleRules.ValidateTask(dto.Name, dto.PlannedStart, dto.PlannedEnd, dto.Weight) is string problem)
                throw new InvalidOperationException(problem);
            var assignee = AssigneeOrNull(schedule.Project, dto.AssigneeUserId);

            var task = new ScheduleTask
            {
                ScheduleId = schedule.Id,
                SortOrder = schedule.Tasks.Count == 0 ? 1 : schedule.Tasks.Max(t => t.SortOrder) + 1,
                Code = (dto.Code ?? string.Empty).Trim(),
                Name = dto.Name.Trim(),
                PlannedStart = dto.PlannedStart.Date,
                PlannedEnd = dto.PlannedEnd.Date,
                Weight = dto.Weight,
                Note = (dto.Note ?? string.Empty).Trim(),
                AssigneeUserId = assignee?.Id,
                Status = ScheduleTaskStatus.NotStarted,
                Progress = 0
            };

            await _schedules.AddTaskAsync(task, new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.ScheduleTask, null,
                $"إضافة بند «{task.Name}» إلى جدول «{schedule.Project.Name}»",
                $"{Format(task.PlannedStart)} → {Format(task.PlannedEnd)}\nالوزن: {task.Weight:0.##}"), schedule.Project.BranchId);

            task.Assignee = assignee;
            return ToTaskDto(task, approved: false, Today);
        }

        public async Task<ScheduleTaskResponseDto> Handle(UpdateScheduleTaskCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var task = await LoadTaskAsync(viewer, request.TaskId);
            var schedule = task.Schedule;
            EnsureDraftEditable(schedule);
            var dto = request.Dto;

            if (ScheduleRules.ValidateTask(dto.Name, dto.PlannedStart, dto.PlannedEnd, dto.Weight) is string problem)
                throw new InvalidOperationException(problem);

            // بند له تواريخ أصلية (منقول من جدول معتمد): تواريخه الحالية لا تتغير إلا بتمديد معتمد
            var start = dto.PlannedStart.Date;
            var end = dto.PlannedEnd.Date;
            if (task.OriginalStart != null && (start != task.PlannedStart.Date || end != task.PlannedEnd.Date))
                throw new InvalidOperationException("تواريخ هذا البند مثبّتة من الجدول المعتمد وتتغير بتمديد معتمد فقط");

            var assignee = AssigneeOrNull(schedule.Project, dto.AssigneeUserId);

            var changes = new List<string>();
            void Track(string label, string? from, string? to)
            {
                if (!string.Equals(from ?? string.Empty, to ?? string.Empty, StringComparison.Ordinal))
                    changes.Add($"{label}: {(string.IsNullOrEmpty(from) ? "—" : from)} ← {(string.IsNullOrEmpty(to) ? "—" : to)}");
            }
            var name = dto.Name.Trim();
            var code = (dto.Code ?? string.Empty).Trim();
            var note = (dto.Note ?? string.Empty).Trim();
            Track("الاسم", task.Name, name);
            Track("الرمز", task.Code, code);
            Track("البداية", Format(task.PlannedStart), Format(start));
            Track("النهاية", Format(task.PlannedEnd), Format(end));
            Track("الوزن", task.Weight.ToString("0.##"), dto.Weight.ToString("0.##"));
            Track("المسؤول", task.Assignee?.FullName, assignee?.FullName);
            if (task.Note != note) changes.Add("الملاحظة: تغيّرت");
            if (changes.Count == 0) return ToTaskDto(task, approved: false, Today);

            task.Name = name;
            task.Code = code;
            task.PlannedStart = start;
            task.PlannedEnd = end;
            task.Weight = dto.Weight;
            task.Note = note;
            task.AssigneeUserId = assignee?.Id;
            task.Assignee = assignee;

            await _schedules.UpdateTaskAsync(task, dto.RowVersion, new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.ScheduleTask,
                task.Id, $"تعديل البند «{task.Name}» في جدول «{schedule.Project.Name}»", string.Join("\n", changes)), schedule.Project.BranchId);
            return ToTaskDto(task, approved: false, Today);
        }

        public async Task<Unit> Handle(DeleteScheduleTaskCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var task = await LoadTaskAsync(viewer, request.TaskId);
            EnsureDraftEditable(task.Schedule);

            await _schedules.DeleteTaskAsync(task, new AuditEntry(viewer.Id, AuditActions.Deleted, AuditEntities.ScheduleTask, task.Id,
                $"حذف البند «{task.Name}» من جدول «{task.Schedule.Project.Name}»",
                $"{Format(task.PlannedStart)} → {Format(task.PlannedEnd)}"), task.Schedule.Project.BranchId);
            return Unit.Value;
        }

        public async Task<Unit> Handle(ReorderScheduleTasksCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var schedule = await LoadScheduleAsync(viewer, request.ScheduleId);
            EnsureDraftEditable(schedule);

            var ids = request.Dto.TaskIds;
            var current = schedule.Tasks.Select(t => t.Id).ToHashSet();
            if (ids.Count != current.Count || ids.Distinct().Count() != ids.Count || !ids.All(current.Contains))
                throw new InvalidOperationException("قائمة الترتيب يجب أن تضم كل بنود المسودة مرة واحدة");

            await _schedules.ReorderAsync(schedule.Id, ids, new AuditEntry(viewer.Id, AuditActions.Reordered, AuditEntities.Schedule, schedule.Id,
                $"إعادة ترتيب بنود الجدول {schedule.VersionNumber} من «{schedule.Project.Name}»"), schedule.Project.BranchId);
            return Unit.Value;
        }

        // ───────── التبعيات ─────────

        public async Task<ScheduleDependencyResponseDto> Handle(AddScheduleDependencyCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var schedule = await LoadScheduleAsync(viewer, request.ScheduleId);
            EnsureDraftEditable(schedule);
            var dto = request.Dto;

            var byId = schedule.Tasks.ToDictionary(t => t.Id);
            if (!byId.TryGetValue(dto.PredecessorId, out var predecessor) || !byId.TryGetValue(dto.SuccessorId, out var successor))
                throw new InvalidOperationException("البندان يجب أن يتبعا هذه المسودة");
            if (dto.PredecessorId == dto.SuccessorId)
                throw new InvalidOperationException("لا يعتمد البند على نفسه");

            var existing = await _schedules.GetDependenciesAsync(schedule.Id);
            if (existing.Any(d => d.PredecessorId == dto.PredecessorId && d.SuccessorId == dto.SuccessorId))
                throw new InvalidOperationException("هذه التبعية مضافة مسبقاً");
            var edges = existing.Select(d => new ScheduleRules.Edge(d.PredecessorId, d.SuccessorId)).ToList();
            if (ScheduleRules.WouldCreateCycle(edges, dto.PredecessorId, dto.SuccessorId))
                throw new InvalidOperationException("هذه التبعية تُنشئ حلقة: يصير البند معتمداً على نفسه بشكل غير مباشر");

            var dependency = new ScheduleDependency { ScheduleId = schedule.Id, PredecessorId = dto.PredecessorId, SuccessorId = dto.SuccessorId };
            await _schedules.AddDependencyAsync(dependency, new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.ScheduleDependency, null,
                $"إضافة تبعية في جدول «{schedule.Project.Name}»", $"«{predecessor.Name}» ← «{successor.Name}»"), schedule.Project.BranchId);

            return new ScheduleDependencyResponseDto
            {
                Id = dependency.Id, PredecessorId = dependency.PredecessorId, SuccessorId = dependency.SuccessorId,
                HasConflict = successor.PlannedStart.Date <= predecessor.PlannedEnd.Date
            };
        }

        public async Task<Unit> Handle(RemoveScheduleDependencyCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var dependency = await _schedules.GetDependencyAsync(request.Id) ?? throw new KeyNotFoundException("التبعية غير موجودة");
            var schedule = dependency.Schedule;
            ProjectRules.EnsureView(viewer, schedule.Project);
            EnsureDraftEditable(schedule);

            await _schedules.RemoveDependencyAsync(dependency, new AuditEntry(viewer.Id, AuditActions.Deleted, AuditEntities.ScheduleDependency,
                dependency.Id, $"حذف تبعية من جدول «{schedule.Project.Name}»",
                $"«{dependency.Predecessor.Name}» ← «{dependency.Successor.Name}»"), schedule.Project.BranchId);
            return Unit.Value;
        }

        // ───────── الإنجاز والتصنيف (الجدول المعتمد) ─────────

        public async Task<ScheduleTaskResponseDto> Handle(UpdateScheduleProgressCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var task = await LoadTaskAsync(viewer, request.TaskId);
            var schedule = task.Schedule;
            ProjectRules.EnsureOpen(schedule.Project);
            if (schedule.Status != ScheduleStatus.Approved)
                throw new InvalidOperationException(schedule.Status == ScheduleStatus.Draft
                    ? "الإنجاز يُحدَّث على الجدول المعتمد: اعتمد المسودة أولاً"
                    : "هذا إصدار سابق من الجدول؛ حدّث الإنجاز على الإصدار المعتمد");

            var dto = request.Dto;
            var today = Today;
            var status = (ScheduleTaskStatus)dto.Status;
            var (actualStart, actualEnd) = ScheduleRules.CompleteActualDates(status, dto.ActualStart ?? task.ActualStart, dto.ActualEnd ?? task.ActualEnd, today);
            if (ScheduleRules.ValidateProgress(status, dto.Progress, actualStart, actualEnd, today) is string problem)
                throw new InvalidOperationException(problem);

            var note = (dto.Note ?? string.Empty).Trim();
            if (dto.Progress < task.Progress && note.Length == 0)
                throw new InvalidOperationException("خفض نسبة الإنجاز يحتاج ملاحظة تشرح السبب");

            var unchanged = status == task.Status && dto.Progress == task.Progress
                            && actualStart?.Date == task.ActualStart?.Date && actualEnd?.Date == task.ActualEnd?.Date;
            if (unchanged && note.Length == 0) return ToTaskDto(task, approved: true, today);

            var oldProgress = task.Progress;
            var oldStatus = task.Status;
            task.Status = status;
            task.Progress = dto.Progress;
            task.ActualStart = actualStart?.Date;
            task.ActualEnd = actualEnd?.Date;

            var entry = new ScheduleProgressEntry
            {
                TaskId = task.Id, Progress = dto.Progress, Status = status, Note = note, UserId = viewer.Id, CreatedAt = DateTime.UtcNow
            };
            await _schedules.SaveProgressAsync(task, entry, dto.RowVersion, new AuditEntry(viewer.Id, AuditActions.Progress, AuditEntities.ScheduleTask,
                task.Id, $"تحديث إنجاز البند «{task.Name}» في «{schedule.Project.Name}»",
                $"النسبة: {oldProgress}% ← {dto.Progress}%\nالحالة: {ScheduleRules.TaskStatusAr(oldStatus)} ← {ScheduleRules.TaskStatusAr(status)}"
                + (note.Length > 0 ? $"\nملاحظة: {note}" : string.Empty)), schedule.Project.BranchId);

            return ToTaskDto(task, approved: true, today);
        }

        public async Task<ScheduleTaskResponseDto> Handle(ClassifyScheduleTaskCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var task = await LoadTaskAsync(viewer, request.TaskId);
            var schedule = task.Schedule;
            ProjectRules.EnsureOpen(schedule.Project);
            if (schedule.Status == ScheduleStatus.Superseded)
                throw new InvalidOperationException("هذا إصدار سابق ومحفوظ للمراجعة فقط");

            var approved = schedule.Status == ScheduleStatus.Approved;
            if (task.IsCritical == request.Dto.IsCritical) return ToTaskDto(task, approved, Today);

            task.IsCritical = request.Dto.IsCritical;
            await _schedules.UpdateTaskAsync(task, request.Dto.RowVersion, new AuditEntry(viewer.Id, AuditActions.Classified, AuditEntities.ScheduleTask,
                task.Id, $"تصنيف البند «{task.Name}» في «{schedule.Project.Name}»", request.Dto.IsCritical ? "حرج" : "غير حرج"), schedule.Project.BranchId);
            return ToTaskDto(task, approved, Today);
        }

        // ───────── مساعدات ─────────

        /// <summary>مسؤول البند من مشاركي المشروع</summary>
        private static User? AssigneeOrNull(Project project, int? userId)
        {
            if (userId is not int id) return null;
            return project.Members.FirstOrDefault(m => m.UserId == id)?.User
                ?? throw new InvalidOperationException("مسؤول البند يُختار من مشاركي المشروع — أضفه مشاركاً أولاً");
        }

        private static string Format(DateTime date) => date.ToString("yyyy-MM-dd");

        private async Task<ScheduleDetailResponseDto> DetailAsync(Viewer viewer, ProjectSchedule schedule)
        {
            var dependencies = await _schedules.GetDependenciesAsync(schedule.Id);
            var startOrder = schedule.Status == ScheduleStatus.Superseded ? null : await _schedules.GetStartOrderAsync(schedule.ProjectId);
            return ToDetailDto(schedule, dependencies, startOrder, viewer, Today);
        }

        public static ScheduleDetailResponseDto ToDetailDto(ProjectSchedule s, IReadOnlyList<ScheduleDependency> dependencies,
            ProjectStartOrder? startOrder, Viewer viewer, DateTime today)
        {
            var project = s.Project;
            var approved = s.Status == ScheduleStatus.Approved;
            var open = !ProjectRules.IsFinished(project.Stage);
            var tasks = s.Tasks.OrderBy(t => t.SortOrder).ThenBy(t => t.Id).ToList();
            var byId = tasks.ToDictionary(t => t.Id);
            var conflicts = ScheduleRules.DependencyConflicts(byId, dependencies.Select(d => new ScheduleRules.Edge(d.PredecessorId, d.SuccessorId)))
                .Select(e => (e.PredecessorId, e.SuccessorId)).ToHashSet();

            var warnings = new List<string>();
            if (s.Status != ScheduleStatus.Superseded)
            {
                if (startOrder == null) warnings.Add("لم يُسجَّل أمر مباشرة لهذا المشروع بعد");
                else
                {
                    var before = tasks.Count(t => t.PlannedStart.Date < startOrder.StartDate.Date);
                    if (before > 0) warnings.Add($"{before} بنداً يبدأ قبل تاريخ المباشرة ({Format(startOrder.StartDate)})");
                    var limit = startOrder.StartDate.Date.AddDays(startOrder.CompletionDays - 1);
                    var after = tasks.Count(t => t.PlannedEnd.Date > limit);
                    if (after > 0) warnings.Add($"{after} بنداً ينتهي بعد مدة الإنجاز في أمر المباشرة ({Format(limit)})");
                }
                if (conflicts.Count > 0) warnings.Add($"{conflicts.Count} تبعية يبدأ فيها اللاحق قبل نهاية سابقه أو في يومها");
            }

            return new ScheduleDetailResponseDto
            {
                Id = s.Id, ProjectId = s.ProjectId, ProjectNumber = ProjectRules.Number(project.Id, project.CreatedAt), ProjectName = project.Name,
                VersionNumber = s.VersionNumber, Title = s.Title, Note = s.Note,
                Status = (int)s.Status, StatusName = ScheduleRules.StatusAr(s.Status),
                CreatedByUserId = s.CreatedByUserId, CreatedByName = s.CreatedByUser?.FullName ?? string.Empty, CreatedAt = s.CreatedAt,
                ApprovedByName = s.ApprovedByUser?.FullName, ApprovedAt = s.ApprovedAt, RowVersion = s.RowVersion,
                ActualProgress = ScheduleRules.ActualProgress(tasks),
                PlannedProgress = ScheduleRules.PlannedProgress(tasks, today),
                BaselineProgress = ScheduleRules.PlannedProgress(tasks, today, useOriginal: true),
                OverdueCandidates = approved ? tasks.Count(t => ScheduleRules.IsOverdueCandidate(t, today)) : 0,
                Warnings = warnings,
                Tasks = tasks.Select(t => ToTaskDto(t, approved, today)).ToList(),
                Dependencies = dependencies.Select(d => new ScheduleDependencyResponseDto
                {
                    Id = d.Id, PredecessorId = d.PredecessorId, SuccessorId = d.SuccessorId,
                    HasConflict = conflicts.Contains((d.PredecessorId, d.SuccessorId))
                }).ToList(),
                CanEdit = s.Status == ScheduleStatus.Draft && open && viewer.Has(AppPermissions.EditProjectSchedule),
                CanApprove = s.Status == ScheduleStatus.Draft && open && viewer.Has(AppPermissions.ApproveProjectSchedule) && s.CreatedByUserId != viewer.Id,
                CanUpdateProgress = approved && open && viewer.Has(AppPermissions.UpdateScheduleProgress),
                CanClassify = s.Status != ScheduleStatus.Superseded && open && viewer.Has(AppPermissions.ClassifyScheduleTask)
            };
        }

        public static ScheduleTaskResponseDto ToTaskDto(ScheduleTask t, bool approved, DateTime today) => new()
        {
            Id = t.Id, SortOrder = t.SortOrder, Code = t.Code, Name = t.Name,
            PlannedStart = t.PlannedStart, PlannedEnd = t.PlannedEnd, OriginalStart = t.OriginalStart, OriginalEnd = t.OriginalEnd,
            ExtraDays = ScheduleRules.ExtraDays(t), Weight = t.Weight, IsCritical = t.IsCritical,
            Status = (int)t.Status, StatusName = ScheduleRules.TaskStatusAr(t.Status), Progress = t.Progress,
            ActualStart = t.ActualStart, ActualEnd = t.ActualEnd, Note = t.Note,
            AssigneeUserId = t.AssigneeUserId, AssigneeName = t.Assignee?.FullName,
            IsOverdueCandidate = approved && ScheduleRules.IsOverdueCandidate(t, today),
            DatesLocked = t.OriginalStart != null, RowVersion = t.RowVersion
        };

        private static ScheduleVersionResponseDto ToVersionDto(ScheduleVersionRow row) => new()
        {
            Id = row.Schedule.Id, VersionNumber = row.Schedule.VersionNumber, Title = row.Schedule.Title, Note = row.Schedule.Note,
            Status = (int)row.Schedule.Status, StatusName = ScheduleRules.StatusAr(row.Schedule.Status),
            CreatedByName = row.Schedule.CreatedByUser?.FullName ?? string.Empty, CreatedAt = row.Schedule.CreatedAt,
            ApprovedByName = row.Schedule.ApprovedByUser?.FullName, ApprovedAt = row.Schedule.ApprovedAt,
            TaskCount = row.TaskCount, ActualProgress = row.ActualProgress
        };

        private static StartOrderResponseDto ToStartOrderDto(ProjectStartOrder o) => new()
        {
            Id = o.Id, ProjectId = o.ProjectId, OrderNumber = o.OrderNumber, OrderDate = o.OrderDate, StartDate = o.StartDate,
            CompletionDays = o.CompletionDays, ExecutionDays = o.ExecutionDays, StudyCost = o.StudyCost,
            SiteEngineerUserId = o.SiteEngineerUserId, SiteEngineerName = o.SiteEngineer?.FullName,
            SectorSupervisorUserId = o.SectorSupervisorUserId, SectorSupervisorName = o.SectorSupervisor?.FullName,
            Notes = o.Notes, CreatedByName = o.CreatedByUser?.FullName ?? string.Empty, CreatedAt = o.CreatedAt, UpdatedAt = o.UpdatedAt,
            RowVersion = o.RowVersion
        };
    }
}
