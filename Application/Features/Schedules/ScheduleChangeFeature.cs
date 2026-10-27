using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.Schedules
{
    // التأخير والتمديد على الجدول المعتمد. الصلاحية (في المتحكم) تفتح العملية، وحدّ السجل هو حدّ رؤية مشروعه.
    // المبادئ: النظام يسجّل ولا يقرّر؛ من سجّل التأخير يرفع تبريره وغيره يعتمده؛ من طلب التمديد لا يعتمده؛
    // التواريخ الحالية لا تتغير إلا باعتماد تمديد، والأصلية لا تتغير أبداً. كل كتابة تُدقَّق في معاملتها.
    public record GetScheduleDelaysQuery(int ProjectId) : IRequest<List<ScheduleDelayResponseDto>>;
    public record GetScheduleDelayQuery(int Id) : IRequest<ScheduleDelayResponseDto>;
    public record RecordScheduleDelayCommand(int TaskId, ScheduleDelayRequestDto Dto) : IRequest<ScheduleDelayResponseDto>;
    public record JustifyScheduleDelayCommand(int Id, JustifyScheduleDelayRequestDto Dto) : IRequest<ScheduleDelayResponseDto>;
    public record DecideScheduleDelayCommand(int Id, ScheduleDecisionRequestDto Dto) : IRequest<ScheduleDelayResponseDto>;

    public record GetScheduleExtensionsQuery(int ProjectId) : IRequest<List<ScheduleExtensionResponseDto>>;
    public record GetScheduleExtensionQuery(int Id) : IRequest<ScheduleExtensionResponseDto>;
    public record PreviewScheduleExtensionCommand(int ProjectId, ScheduleExtensionRequestDto Dto) : IRequest<ScheduleExtensionPreviewResponseDto>;
    public record CreateScheduleExtensionCommand(int ProjectId, ScheduleExtensionRequestDto Dto) : IRequest<ScheduleExtensionResponseDto>;
    public record UpdateScheduleExtensionCommand(int Id, ScheduleExtensionRequestDto Dto) : IRequest<ScheduleExtensionResponseDto>;
    public record SubmitScheduleExtensionCommand(int Id) : IRequest<ScheduleExtensionResponseDto>;
    public record DeleteScheduleExtensionCommand(int Id) : IRequest<Unit>;
    public record DecideScheduleExtensionCommand(int Id, ScheduleDecisionRequestDto Dto) : IRequest<ScheduleExtensionResponseDto>;

    // ════════════════════ التحقق من المدخلات (الأطوال؛ القيم في ScheduleRules) ════════════════════

    public class RecordScheduleDelayCommandValidator : AbstractValidator<RecordScheduleDelayCommand>
    {
        public RecordScheduleDelayCommandValidator()
        {
            RuleFor(x => x.Dto.Kind).Must(k => Enum.IsDefined(typeof(DelayKind), k)).WithMessage("نوع التأخير غير صحيح");
            RuleFor(x => x.Dto.Reason).MaximumLength(2000).WithMessage("السبب لا يتجاوز 2000 حرف");
            RuleFor(x => x.Dto.Actions).MaximumLength(2000).WithMessage("الإجراءات لا تتجاوز 2000 حرف");
        }
    }

    public class JustifyScheduleDelayCommandValidator : AbstractValidator<JustifyScheduleDelayCommand>
    {
        public JustifyScheduleDelayCommandValidator()
        {
            RuleFor(x => x.Dto.Reason).MaximumLength(2000).WithMessage("السبب لا يتجاوز 2000 حرف");
            RuleFor(x => x.Dto.Actions).MaximumLength(2000).WithMessage("الإجراءات لا تتجاوز 2000 حرف");
            RuleFor(x => x.Dto.ApprovalAuthority).MaximumLength(200).WithMessage("اسم الجهة لا يتجاوز 200 حرف");
        }
    }

    public class DecideScheduleDelayCommandValidator : AbstractValidator<DecideScheduleDelayCommand>
    {
        public DecideScheduleDelayCommandValidator() =>
            RuleFor(x => x.Dto.Note).MaximumLength(1000).WithMessage("الملاحظة لا تتجاوز 1000 حرف");
    }

    public class DecideScheduleExtensionCommandValidator : AbstractValidator<DecideScheduleExtensionCommand>
    {
        public DecideScheduleExtensionCommandValidator() =>
            RuleFor(x => x.Dto.Note).MaximumLength(1000).WithMessage("الملاحظة لا تتجاوز 1000 حرف");
    }

    public class ScheduleExtensionRequestDtoValidator : AbstractValidator<ScheduleExtensionRequestDto>
    {
        public ScheduleExtensionRequestDtoValidator()
        {
            RuleFor(x => x.Mode).Must(m => Enum.IsDefined(typeof(ExtensionMode), m)).WithMessage("نوع التمديد غير صحيح");
            RuleFor(x => x.DayCount).Must(c => Enum.IsDefined(typeof(ExtensionDayCount), c)).WithMessage("طريقة عدّ الأيام غير صحيحة");
            RuleFor(x => x.Reason).MaximumLength(2000).WithMessage("سبب التمديد لا يتجاوز 2000 حرف");
            RuleFor(x => x.Lines).Must(l => l.Count <= ScheduleRules.MaxTasks).WithMessage("عدد بنود التمديد كبير جداً");
            RuleFor(x => x.DocumentVersionIds).Must(d => d.Count <= 20).WithMessage("المستندات الداعمة حتى 20 مستنداً");
        }
    }

    public class PreviewScheduleExtensionCommandValidator : AbstractValidator<PreviewScheduleExtensionCommand>
    {
        public PreviewScheduleExtensionCommandValidator() => RuleFor(x => x.Dto).SetValidator(new ScheduleExtensionRequestDtoValidator());
    }

    public class CreateScheduleExtensionCommandValidator : AbstractValidator<CreateScheduleExtensionCommand>
    {
        public CreateScheduleExtensionCommandValidator()
        {
            RuleFor(x => x.Dto).SetValidator(new ScheduleExtensionRequestDtoValidator());
            RuleFor(x => x.Dto.Reason).NotEmpty().WithMessage("سبب التمديد مطلوب");
        }
    }

    public class UpdateScheduleExtensionCommandValidator : AbstractValidator<UpdateScheduleExtensionCommand>
    {
        public UpdateScheduleExtensionCommandValidator()
        {
            RuleFor(x => x.Dto).SetValidator(new ScheduleExtensionRequestDtoValidator());
            RuleFor(x => x.Dto.Reason).NotEmpty().WithMessage("سبب التمديد مطلوب");
        }
    }

    // ════════════════════ المعالج ════════════════════

    public class ScheduleChangeHandler :
        IRequestHandler<GetScheduleDelaysQuery, List<ScheduleDelayResponseDto>>,
        IRequestHandler<GetScheduleDelayQuery, ScheduleDelayResponseDto>,
        IRequestHandler<RecordScheduleDelayCommand, ScheduleDelayResponseDto>,
        IRequestHandler<JustifyScheduleDelayCommand, ScheduleDelayResponseDto>,
        IRequestHandler<DecideScheduleDelayCommand, ScheduleDelayResponseDto>,
        IRequestHandler<GetScheduleExtensionsQuery, List<ScheduleExtensionResponseDto>>,
        IRequestHandler<GetScheduleExtensionQuery, ScheduleExtensionResponseDto>,
        IRequestHandler<PreviewScheduleExtensionCommand, ScheduleExtensionPreviewResponseDto>,
        IRequestHandler<CreateScheduleExtensionCommand, ScheduleExtensionResponseDto>,
        IRequestHandler<UpdateScheduleExtensionCommand, ScheduleExtensionResponseDto>,
        IRequestHandler<SubmitScheduleExtensionCommand, ScheduleExtensionResponseDto>,
        IRequestHandler<DeleteScheduleExtensionCommand, Unit>,
        IRequestHandler<DecideScheduleExtensionCommand, ScheduleExtensionResponseDto>
    {
        private readonly IScheduleChangeService _changes;
        private readonly IScheduleService _schedules;
        private readonly IProjectService _projects;
        private readonly IProjectDocumentService _documents;
        private readonly IPublicHolidayService _holidays;
        private readonly INotificationService _notifications;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;

        public ScheduleChangeHandler(IScheduleChangeService changes, IScheduleService schedules, IProjectService projects,
            IProjectDocumentService documents, IPublicHolidayService holidays, INotificationService notifications,
            IUserService users, IUserPermissionService permissions)
        {
            _changes = changes;
            _schedules = schedules;
            _projects = projects;
            _documents = documents;
            _holidays = holidays;
            _notifications = notifications;
            _users = users;
            _permissions = permissions;
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_users, _permissions);

        private static DateTime Today => DateTime.Today;

        private static string Format(DateTime date) => date.ToString("yyyy-MM-dd");

        // ───────── التحميل بعد فحص الحدّ ─────────

        private async Task<Project> LoadProjectAsync(Viewer viewer, int projectId)
        {
            var project = await _projects.GetByIdAsync(projectId) ?? throw new KeyNotFoundException("المشروع غير موجود");
            ProjectRules.EnsureView(viewer, project);
            return project;
        }

        private async Task<ScheduleDelay> LoadDelayAsync(Viewer viewer, int id)
        {
            var delay = await _changes.GetDelayAsync(id) ?? throw new KeyNotFoundException("التأخير غير موجود");
            ProjectRules.EnsureView(viewer, delay.Project);
            return delay;
        }

        private async Task<ScheduleExtension> LoadExtensionAsync(Viewer viewer, int id)
        {
            var extension = await _changes.GetExtensionAsync(id) ?? throw new KeyNotFoundException("طلب التمديد غير موجود");
            ProjectRules.EnsureView(viewer, extension.Project);
            return extension;
        }

        /// <summary>إصدار التأخير أو التمديد يجب أن يكون هو المعتمد الحالي (التواريخ الحية فيه)</summary>
        private static void EnsureApproved(ProjectSchedule schedule, string what)
        {
            if (schedule.Status != ScheduleStatus.Approved)
                throw new InvalidOperationException($"{what} على إصدار سابق من الجدول — لا يُعالج إلا على الجدول المعتمد الحالي");
        }

        // ════════════════════ التأخير ════════════════════

        public async Task<List<ScheduleDelayResponseDto>> Handle(GetScheduleDelaysQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var project = await LoadProjectAsync(viewer, request.ProjectId);
            var delays = await _changes.GetDelaysAsync(project.Id, take: 300);
            return delays.Select(d => ToDelayDto(d, project, viewer, suggested: null)).ToList();
        }

        public async Task<ScheduleDelayResponseDto> Handle(GetScheduleDelayQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            return await DelayDetailAsync(viewer, await LoadDelayAsync(viewer, request.Id));
        }

        public async Task<ScheduleDelayResponseDto> Handle(RecordScheduleDelayCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var task = await _schedules.GetTaskAsync(request.TaskId) ?? throw new KeyNotFoundException("البند غير موجود");
            var project = task.Schedule.Project;
            ProjectRules.EnsureView(viewer, project);
            ProjectRules.EnsureOpen(project);
            EnsureApproved(task.Schedule, "التأخير يُسجَّل");
            var dto = request.Dto;
            var kind = (DelayKind)dto.Kind;

            if (ScheduleRules.ValidateDelayRecord(task, kind, dto.DiscoveredAt, dto.Reason, dto.Actions, Today) is string problem)
                throw new InvalidOperationException(problem);
            if (await _changes.HasLiveDelayAsync(task.Id))
                throw new InvalidOperationException("يوجد تأخير قائم على هذا البند: عالجه (برّره أو مدّد البند) قبل تسجيل غيره");

            var affected = await ResolveAffectedAsync(task.ScheduleId, task.Id, dto.AffectedTaskIds);
            var delay = new ScheduleDelay
            {
                ProjectId = project.Id,
                TaskId = task.Id,
                Kind = kind,
                Status = DelayStatus.Recorded,
                DiscoveredAt = dto.DiscoveredAt.Date,
                PlannedEndAtDiscovery = task.PlannedEnd.Date,
                Reason = (dto.Reason ?? string.Empty).Trim(),
                Actions = (dto.Actions ?? string.Empty).Trim(),
                RecordedByUserId = viewer.Id,
                RecordedAt = DateTime.UtcNow,
                AffectedTasks = affected.Select(t => new ScheduleDelayAffectedTask { TaskId = t.Id }).ToList()
            };

            var late = Math.Max(0, (delay.DiscoveredAt - delay.PlannedEndAtDiscovery).Days);
            await _changes.AddDelayAsync(delay, new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.Delay, null,
                $"تسجيل تأخير على «{task.Name}» في «{project.Name}»",
                $"النوع: {ScheduleRules.DelayKindAr(kind)}\nنهاية البند المخططة: {Format(delay.PlannedEndAtDiscovery)}\nتاريخ الاكتشاف: {Format(delay.DiscoveredAt)} ({late} يوماً)"
                + (affected.Count > 0 ? $"\nمتأثرة: {string.Join("، ", affected.Select(t => t.Name))}" : string.Empty)), project.BranchId);

            return await DelayDetailAsync(viewer, await LoadDelayAsync(viewer, delay.Id));
        }

        public async Task<ScheduleDelayResponseDto> Handle(JustifyScheduleDelayCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var delay = await LoadDelayAsync(viewer, request.Id);
            var project = delay.Project;
            ProjectRules.EnsureOpen(project);
            EnsureApproved(delay.Task.Schedule, "التأخير مسجَّل");
            var dto = request.Dto;

            // من سجّل التأخير يرفع تبريره وحده، كي لا يعتمد من رفعه تبريره بنفسه (الاعتماد لغير المسجِّل)
            if (delay.RecordedByUserId != viewer.Id)
                throw new InvalidOperationException("يرفع تبرير التأخير من سجّله");
            if (delay.Status is not (DelayStatus.Recorded or DelayStatus.Rejected))
                throw new InvalidOperationException("لا يُرفع تبرير لهذا التأخير في حالته الحالية");

            if (ScheduleRules.ValidateJustification(dto.Reason, dto.ApprovalAuthority, dto.ApprovalDate, dto.ApprovalDocumentVersionId, Today) is string problem)
                throw new InvalidOperationException(problem);
            var version = await _documents.GetVersionAsync(dto.ApprovalDocumentVersionId!.Value) ?? throw new KeyNotFoundException("مستند الموافقة غير موجود");
            if (version.Document.ProjectId != project.Id)
                throw new InvalidOperationException("مستند الموافقة يجب أن يكون من وثائق هذا المشروع");

            var affected = await ResolveAffectedAsync(delay.Task.ScheduleId, delay.TaskId, dto.AffectedTaskIds);
            SyncAffected(delay, affected);

            delay.Kind = DelayKind.Justified;
            delay.Status = DelayStatus.Submitted;
            delay.Reason = dto.Reason!.Trim();
            delay.Actions = (dto.Actions ?? string.Empty).Trim();
            delay.ApprovalAuthority = dto.ApprovalAuthority!.Trim();
            delay.ApprovalDate = dto.ApprovalDate!.Value.Date;
            delay.ApprovalDocumentVersionId = version.Id;
            delay.ApprovalDocumentVersion = version;
            delay.AffectsCriticalWork = dto.AffectsCriticalWork;
            delay.DecidedByUserId = null;
            delay.DecidedByUser = null;
            delay.DecidedAt = null;
            delay.DecisionNote = string.Empty;

            await _changes.SaveDelayAsync(delay, dto.RowVersion, new AuditEntry(viewer.Id, AuditActions.Justified, AuditEntities.Delay, delay.Id,
                $"رفع تبرير تأخير «{delay.Task.Name}» في «{project.Name}»",
                $"السبب: {delay.Reason}\nالجهة الموافقة: {delay.ApprovalAuthority} ({Format(delay.ApprovalDate.Value)})\nالمستند: {version.Document.Title} — {version.FileName}"
                + (delay.AffectsCriticalWork ? "\nيؤثر على أعمال حرجة" : string.Empty)), project.BranchId);

            await ScheduleNotifier.SendAsync(_notifications, await ScheduleNotifier.HoldersAsync(_permissions, project, AppPermissions.ApproveDelayJustification),
                viewer.Id, project, NotificationType.ScheduleJustificationSubmitted, "تبرير تأخير بانتظار الاعتماد",
                $"رفع {viewer.User.FullName} تبرير تأخير البند «{delay.Task.Name}» في المشروع {ScheduleNotifier.Label(project)}");

            return await DelayDetailAsync(viewer, await LoadDelayAsync(viewer, delay.Id));
        }

        public async Task<ScheduleDelayResponseDto> Handle(DecideScheduleDelayCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var delay = await LoadDelayAsync(viewer, request.Id);
            var project = delay.Project;
            ProjectRules.EnsureOpen(project);
            var dto = request.Dto;
            ScheduleRules.EnsureCanDecideJustification(viewer, delay, dto.Approve, dto.Note);

            delay.Status = dto.Approve ? DelayStatus.Approved : DelayStatus.Rejected;
            delay.DecidedByUserId = viewer.Id;
            delay.DecidedByUser = viewer.User;
            delay.DecidedAt = DateTime.UtcNow;
            delay.DecisionNote = (dto.Note ?? string.Empty).Trim();

            await _changes.SaveDelayAsync(delay, dto.RowVersion, new AuditEntry(viewer.Id, dto.Approve ? AuditActions.Approved : AuditActions.Rejected,
                AuditEntities.Delay, delay.Id, $"{(dto.Approve ? "اعتماد" : "رفض")} تبرير تأخير «{delay.Task.Name}» في «{project.Name}»",
                delay.DecisionNote), project.BranchId);

            await ScheduleNotifier.SendAsync(_notifications, [delay.RecordedByUserId], viewer.Id, project, NotificationType.ScheduleJustificationDecided,
                dto.Approve ? "اعتُمد تبرير التأخير" : "رُفض تبرير التأخير",
                $"{(dto.Approve ? "اعتمد" : "رفض")} {viewer.User.FullName} تبرير تأخير البند «{delay.Task.Name}» في المشروع {ScheduleNotifier.Label(project)}"
                + (delay.DecisionNote.Length > 0 ? $" — {delay.DecisionNote}" : string.Empty));

            return await DelayDetailAsync(viewer, delay);
        }

        /// <summary>البنود المتأثرة من الجدول نفسه، غير البند المتأخر نفسه، وبلا تكرار</summary>
        private async Task<List<ScheduleTask>> ResolveAffectedAsync(int scheduleId, int delayedTaskId, IReadOnlyCollection<int> ids)
        {
            var wanted = ids.Distinct().ToList();
            if (wanted.Count == 0) return [];
            var schedule = await _schedules.GetByIdAsync(scheduleId) ?? throw new KeyNotFoundException("الجدول غير موجود");
            var byId = schedule.Tasks.ToDictionary(t => t.Id);
            if (wanted.Contains(delayedTaskId)) throw new InvalidOperationException("البند المتأخر نفسه ليس من البنود المتأثرة");
            if (wanted.Any(id => !byId.ContainsKey(id))) throw new InvalidOperationException("بند متأثر لا يتبع هذا الجدول");
            return wanted.Select(id => byId[id]).ToList();
        }

        /// <summary>يحدّث البنود المتأثرة بالفرق فقط (مفتاح الجدول مركّب، فإعادة إضافة بند محذوف في الحفظ نفسه تُربك التتبع)</summary>
        private static void SyncAffected(ScheduleDelay delay, IReadOnlyList<ScheduleTask> wanted)
        {
            var keep = wanted.Select(t => t.Id).ToHashSet();
            foreach (var old in delay.AffectedTasks.Where(a => !keep.Contains(a.TaskId)).ToList()) delay.AffectedTasks.Remove(old);
            var have = delay.AffectedTasks.Select(a => a.TaskId).ToHashSet();
            foreach (var id in keep.Where(id => !have.Contains(id))) delay.AffectedTasks.Add(new ScheduleDelayAffectedTask { DelayId = delay.Id, TaskId = id });
        }

        private async Task<ScheduleDelayResponseDto> DelayDetailAsync(Viewer viewer, ScheduleDelay delay)
        {
            // لواحق مقترحة للمراجعة: لتأخير حيّ على الجدول المعتمد فقط
            List<ScheduleAffectedTaskDto>? suggested = null;
            if (delay.Status != DelayStatus.Resolved && delay.Task.Schedule.Status == ScheduleStatus.Approved)
            {
                var schedule = await _schedules.GetByIdAsync(delay.Task.ScheduleId);
                if (schedule != null)
                {
                    var edges = (await _schedules.GetDependenciesAsync(schedule.Id)).Select(d => new ScheduleRules.Edge(d.PredecessorId, d.SuccessorId));
                    var byId = schedule.Tasks.ToDictionary(t => t.Id);
                    var already = delay.AffectedTasks.Select(a => a.TaskId).ToHashSet();
                    suggested = ScheduleRules.SuggestedSuccessors([delay.TaskId], edges, byId)
                        .Where(id => !already.Contains(id))
                        .Select(id => new ScheduleAffectedTaskDto { TaskId = id, Code = byId[id].Code, Name = byId[id].Name }).ToList();
                }
            }
            return ToDelayDto(delay, delay.Project, viewer, suggested);
        }

        private static ScheduleDelayResponseDto ToDelayDto(ScheduleDelay d, Project project, Viewer viewer, List<ScheduleAffectedTaskDto>? suggested)
        {
            var open = !ProjectRules.IsFinished(project.Stage);
            var approvedSchedule = d.Task.Schedule?.Status == ScheduleStatus.Approved;
            return new ScheduleDelayResponseDto
            {
                Id = d.Id, ProjectId = d.ProjectId, TaskId = d.TaskId, TaskCode = d.Task.Code, TaskName = d.Task.Name,
                ScheduleId = d.Task.ScheduleId, ScheduleVersion = d.Task.Schedule?.VersionNumber ?? 0,
                Kind = (int)d.Kind, KindName = ScheduleRules.DelayKindAr(d.Kind),
                Status = (int)d.Status, StatusName = ScheduleRules.DelayStatusAr(d.Status),
                DiscoveredAt = d.DiscoveredAt, PlannedEndAtDiscovery = d.PlannedEndAtDiscovery,
                DaysLate = Math.Max(0, (d.DiscoveredAt.Date - d.PlannedEndAtDiscovery.Date).Days),
                Reason = d.Reason, Actions = d.Actions, ApprovalAuthority = d.ApprovalAuthority, ApprovalDate = d.ApprovalDate,
                ApprovalDocumentVersionId = d.ApprovalDocumentVersionId, ApprovalDocumentTitle = d.ApprovalDocumentVersion?.Document?.Title,
                AffectsCriticalWork = d.AffectsCriticalWork,
                AffectedTasks = d.AffectedTasks.Select(a => new ScheduleAffectedTaskDto { TaskId = a.TaskId, Code = a.Task?.Code ?? string.Empty, Name = a.Task?.Name ?? string.Empty }).ToList(),
                SuggestedTasks = suggested,
                RecordedByUserId = d.RecordedByUserId, RecordedByName = d.RecordedByUser?.FullName ?? string.Empty, RecordedAt = d.RecordedAt,
                DecidedByName = d.DecidedByUser?.FullName, DecidedAt = d.DecidedAt, DecisionNote = d.DecisionNote, RowVersion = d.RowVersion,
                CanJustify = open && approvedSchedule && viewer.Has(AppPermissions.RecordScheduleDelay) && d.RecordedByUserId == viewer.Id
                             && d.Status is DelayStatus.Recorded or DelayStatus.Rejected,
                CanDecide = open && viewer.Has(AppPermissions.ApproveDelayJustification) && d.Kind == DelayKind.Justified
                            && d.Status == DelayStatus.Submitted && d.RecordedByUserId != viewer.Id
            };
        }

        // ════════════════════ التمديد ════════════════════

        public async Task<List<ScheduleExtensionResponseDto>> Handle(GetScheduleExtensionsQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var project = await LoadProjectAsync(viewer, request.ProjectId);
            var extensions = await _changes.GetExtensionsAsync(project.Id, take: 100);
            return extensions.Select(x => ToExtensionDto(x, project, viewer, null, [], [])).ToList();
        }

        public async Task<ScheduleExtensionResponseDto> Handle(GetScheduleExtensionQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            return await ExtensionDetailAsync(viewer, await LoadExtensionAsync(viewer, request.Id));
        }

        public async Task<ScheduleExtensionPreviewResponseDto> Handle(PreviewScheduleExtensionCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var project = await LoadProjectAsync(viewer, request.ProjectId);
            ProjectRules.EnsureOpen(project);
            var (schedule, byId, preview) = await BuildPreviewAsync(project, request.Dto);

            var edges = (await _schedules.GetDependenciesAsync(schedule.Id)).Select(d => new ScheduleRules.Edge(d.PredecessorId, d.SuccessorId)).ToList();
            return new ScheduleExtensionPreviewResponseDto
            {
                Items = preview.Select(p => ToItemDto(p, byId)).ToList(),
                Conflicts = ToConflicts(ScheduleRules.PreviewConflicts(preview, byId, edges), byId),
                SuggestedTasks = ToSuggested(ScheduleRules.SuggestedSuccessors(preview.Select(p => p.TaskId), edges, byId), byId)
            };
        }

        public async Task<ScheduleExtensionResponseDto> Handle(CreateScheduleExtensionCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var project = await LoadProjectAsync(viewer, request.ProjectId);
            ProjectRules.EnsureOpen(project);
            if (await _schedules.HasPendingExtensionAsync(project.Id))
                throw new InvalidOperationException("يوجد طلب تمديد لم يُبتّ فيه لهذا المشروع: اعتمده أو ارفضه أو احذفه قبل إعداد طلب جديد");

            var dto = request.Dto;
            var (schedule, _, preview) = await BuildPreviewAsync(project, dto);
            var delay = await ResolveDelayAsync(project, schedule, dto.DelayId);
            var versions = await ResolveDocumentsAsync(project, dto.DocumentVersionIds);

            var extension = new ScheduleExtension
            {
                ProjectId = project.Id,
                ScheduleId = schedule.Id,
                DelayId = delay?.Id,
                Mode = (ExtensionMode)dto.Mode,
                DayCount = (ExtensionDayCount)dto.DayCount,
                Status = ExtensionStatus.Draft,
                Reason = dto.Reason!.Trim(),
                RequestedByUserId = viewer.Id,
                RequestedAt = DateTime.UtcNow,
                Items = preview.Select(ToItem).ToList(),
                Documents = versions.Select(v => new ScheduleExtensionDocument { DocumentVersionId = v.Id }).ToList()
            };

            await _changes.AddExtensionAsync(extension, new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.Extension, null,
                $"إعداد طلب تمديد لجدول «{project.Name}»", ExtensionSummary(extension, preview)), project.BranchId);

            return await ExtensionDetailAsync(viewer, await LoadExtensionAsync(viewer, extension.Id));
        }

        public async Task<ScheduleExtensionResponseDto> Handle(UpdateScheduleExtensionCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var extension = await LoadExtensionAsync(viewer, request.Id);
            var project = extension.Project;
            ProjectRules.EnsureOpen(project);
            EnsureRequesterDraft(viewer, extension);
            var dto = request.Dto;

            var (schedule, _, preview) = await BuildPreviewAsync(project, dto);
            var delay = await ResolveDelayAsync(project, schedule, dto.DelayId);
            var versions = await ResolveDocumentsAsync(project, dto.DocumentVersionIds);

            extension.DelayId = delay?.Id;
            extension.Mode = (ExtensionMode)dto.Mode;
            extension.DayCount = (ExtensionDayCount)dto.DayCount;
            extension.Reason = dto.Reason!.Trim();

            // البنود تُستبدل (لكل سطر معرّف خاص)، والمستندات بالفرق (مفتاحها مركّب)
            foreach (var old in extension.Items.ToList()) extension.Items.Remove(old);
            foreach (var item in preview.Select(ToItem)) extension.Items.Add(item);
            var keep = versions.Select(v => v.Id).ToHashSet();
            foreach (var old in extension.Documents.Where(d => !keep.Contains(d.DocumentVersionId)).ToList()) extension.Documents.Remove(old);
            var have = extension.Documents.Select(d => d.DocumentVersionId).ToHashSet();
            foreach (var id in keep.Where(id => !have.Contains(id)))
                extension.Documents.Add(new ScheduleExtensionDocument { ExtensionId = extension.Id, DocumentVersionId = id });

            await _changes.SaveExtensionAsync(extension, dto.RowVersion, new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.Extension, extension.Id,
                $"تعديل طلب تمديد لجدول «{project.Name}»", ExtensionSummary(extension, preview)), project.BranchId);

            return await ExtensionDetailAsync(viewer, await LoadExtensionAsync(viewer, extension.Id));
        }

        public async Task<ScheduleExtensionResponseDto> Handle(SubmitScheduleExtensionCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var extension = await LoadExtensionAsync(viewer, request.Id);
            var project = extension.Project;
            ProjectRules.EnsureOpen(project);
            EnsureRequesterDraft(viewer, extension);
            EnsureApproved(extension.Schedule, "الطلب مُعدّ");

            // لا يُرفع طلب تغيّرت تواريخ بنوده بعد إعداده: يُعاد إعداده ليُعاين من جديد
            var schedule = await _schedules.GetByIdAsync(extension.ScheduleId) ?? throw new KeyNotFoundException("الجدول غير موجود");
            if (ScheduleRules.StalePreviewProblem(extension.Items, schedule.Tasks.ToDictionary(t => t.Id)) is string stale)
                throw new InvalidOperationException(stale);

            extension.Status = ExtensionStatus.Submitted;
            extension.RequestedAt = DateTime.UtcNow;
            await _changes.SaveExtensionAsync(extension, null, new AuditEntry(viewer.Id, AuditActions.Submitted, AuditEntities.Extension, extension.Id,
                $"رفع طلب تمديد لجدول «{project.Name}» للاعتماد", $"البنود: {extension.Items.Count}"), project.BranchId);

            await ScheduleNotifier.SendAsync(_notifications, await ScheduleNotifier.HoldersAsync(_permissions, project, AppPermissions.ApproveScheduleExtension),
                viewer.Id, project, NotificationType.ScheduleExtensionSubmitted, "طلب تمديد بانتظار الاعتماد",
                $"رفع {viewer.User.FullName} طلب تمديد على جدول المشروع {ScheduleNotifier.Label(project)} ({extension.Items.Count} بنداً)");

            return await ExtensionDetailAsync(viewer, extension);
        }

        public async Task<Unit> Handle(DeleteScheduleExtensionCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var extension = await LoadExtensionAsync(viewer, request.Id);
            ProjectRules.EnsureOpen(extension.Project);
            EnsureRequesterDraft(viewer, extension);

            await _changes.DeleteExtensionAsync(extension, new AuditEntry(viewer.Id, AuditActions.Deleted, AuditEntities.Extension, extension.Id,
                $"حذف مسودة طلب تمديد لجدول «{extension.Project.Name}»", $"البنود: {extension.Items.Count}"), extension.Project.BranchId);
            return Unit.Value;
        }

        public async Task<ScheduleExtensionResponseDto> Handle(DecideScheduleExtensionCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var extension = await LoadExtensionAsync(viewer, request.Id);
            var project = extension.Project;
            ProjectRules.EnsureOpen(project);
            var dto = request.Dto;
            ScheduleRules.EnsureCanDecideExtension(viewer, extension);
            if (!dto.Approve && string.IsNullOrWhiteSpace(dto.Note))
                throw new InvalidOperationException("اذكر سبب رفض التمديد");

            var details = new List<string>();
            if (dto.Approve)
            {
                EnsureApproved(extension.Schedule, "الطلب مُعدّ");
                var schedule = await _schedules.GetByIdAsync(extension.ScheduleId) ?? throw new KeyNotFoundException("الجدول غير موجود");
                var byId = schedule.Tasks.ToDictionary(t => t.Id);
                if (ScheduleRules.StalePreviewProblem(extension.Items, byId) is string stale) throw new InvalidOperationException(stale);

                details.AddRange(extension.Items.Select(i => $"«{byId[i.TaskId].Name}»: {Format(i.OldEnd)} ← {Format(i.NewEnd)} (+{i.Days})"));
                ScheduleRules.ApplyExtension(extension.Items, byId);      // الأصلية لا تُمسّ: الحالية فقط

                // التأخيرات القائمة على البنود الممدودة ينتهي أثرها
                var resolvable = await _changes.GetResolvableDelaysAsync(extension.Items.Select(i => i.TaskId).ToList());
                foreach (var delay in resolvable) delay.Status = DelayStatus.Resolved;
            }
            else details.Add(dto.Note!.Trim());

            extension.Status = dto.Approve ? ExtensionStatus.Approved : ExtensionStatus.Rejected;
            extension.DecidedByUserId = viewer.Id;
            extension.DecidedByUser = viewer.User;
            extension.DecidedAt = DateTime.UtcNow;
            extension.DecisionNote = (dto.Note ?? string.Empty).Trim();

            await _changes.SaveExtensionAsync(extension, dto.RowVersion, new AuditEntry(viewer.Id, dto.Approve ? AuditActions.Approved : AuditActions.Rejected,
                AuditEntities.Extension, extension.Id, $"{(dto.Approve ? "اعتماد" : "رفض")} طلب تمديد لجدول «{project.Name}»",
                string.Join("\n", details)), project.BranchId);

            await ScheduleNotifier.SendAsync(_notifications, [extension.RequestedByUserId], viewer.Id, project, NotificationType.ScheduleExtensionDecided,
                dto.Approve ? "اعتُمد التمديد" : "رُفض التمديد",
                $"{(dto.Approve ? "اعتمد" : "رفض")} {viewer.User.FullName} طلب التمديد على جدول المشروع {ScheduleNotifier.Label(project)}"
                + (extension.DecisionNote.Length > 0 ? $" — {extension.DecisionNote}" : string.Empty));

            return await ExtensionDetailAsync(viewer, extension);
        }

        // ───────── مساعدات التمديد ─────────

        /// <summary>طلب في المسودة وصاحبه وحده يعدّله أو يرفعه أو يحذفه (فصل الطلب عن الاعتماد)</summary>
        private static void EnsureRequesterDraft(Viewer viewer, ScheduleExtension extension)
        {
            if (extension.Status != ExtensionStatus.Draft)
                throw new InvalidOperationException("الطلب لم يعد مسودة");
            if (extension.RequestedByUserId != viewer.Id)
                throw new InvalidOperationException("يعدّل طلب التمديد صاحبه وحده");
        }

        /// <summary>
        /// معاينة سطور الطلب على التواريخ الحالية للجدول المعتمد: تتحقق من السطور (بند مكرر، مكتمل، أيام خارج الحد، جماعي بأيام مختلفة)
        /// وتحسب التواريخ الجديدة بالأيام التقويمية أو بأيام العمل (بالعطل الرسمية).
        /// </summary>
        private async Task<(ProjectSchedule Schedule, Dictionary<int, ScheduleTask> ById, List<ScheduleRules.ExtensionPreviewItem> Preview)> BuildPreviewAsync(
            Project project, ScheduleExtensionRequestDto dto)
        {
            var schedule = await _schedules.GetApprovedAsync(project.Id)
                ?? throw new InvalidOperationException("لا جدول معتمد لهذا المشروع — يُمدَّد الجدول المعتمد فقط");
            var byId = schedule.Tasks.ToDictionary(t => t.Id);
            var lines = dto.Lines.Select(l => new ScheduleRules.ExtensionLine(l.TaskId, l.Days, l.MovesStart)).ToList();
            var mode = (ExtensionMode)dto.Mode;
            var count = (ExtensionDayCount)dto.DayCount;

            if (ScheduleRules.ValidateExtensionLines(lines, byId, mode) is string problem) throw new InvalidOperationException(problem);

            IReadOnlySet<DateTime> holidays = new HashSet<DateTime>();
            if (count == ExtensionDayCount.WorkingDays)
            {
                var chosen = lines.Select(l => byId[l.TaskId]).ToList();
                holidays = await _holidays.GetDatesAsync(chosen.Min(t => t.PlannedStart).Date, chosen.Max(t => t.PlannedEnd).Date.AddDays(800));
            }
            return (schedule, byId, ScheduleRules.Preview(lines, byId, count, holidays));
        }

        /// <summary>التأخير المربوط: من هذا المشروع، على الجدول المعتمد الحالي، واعتُمد تبريره</summary>
        private async Task<ScheduleDelay?> ResolveDelayAsync(Project project, ProjectSchedule schedule, int? delayId)
        {
            if (delayId is not int id) return null;
            var delay = await _changes.GetDelayAsync(id) ?? throw new KeyNotFoundException("التأخير غير موجود");
            if (delay.ProjectId != project.Id || delay.Task.ScheduleId != schedule.Id)
                throw new InvalidOperationException("التأخير لا يتبع الجدول المعتمد الحالي لهذا المشروع");
            if (delay.Status != DelayStatus.Approved)
                throw new InvalidOperationException("يُربط بالتمديد تأخير اعتُمد تبريره فقط");
            return delay;
        }

        private async Task<List<ProjectDocumentVersion>> ResolveDocumentsAsync(Project project, IEnumerable<int> ids)
        {
            var result = new List<ProjectDocumentVersion>();
            foreach (var id in ids.Distinct())
            {
                var version = await _documents.GetVersionAsync(id) ?? throw new KeyNotFoundException("مستند داعم غير موجود");
                if (version.Document.ProjectId != project.Id)
                    throw new InvalidOperationException("المستندات الداعمة من وثائق هذا المشروع فقط");
                result.Add(version);
            }
            return result;
        }

        private static ScheduleExtensionItem ToItem(ScheduleRules.ExtensionPreviewItem p) => new()
        {
            TaskId = p.TaskId, Days = p.Days, MovesStart = p.MovesStart,
            OldStart = p.OldStart, OldEnd = p.OldEnd, NewStart = p.NewStart, NewEnd = p.NewEnd
        };

        private static string ExtensionSummary(ScheduleExtension x, IEnumerable<ScheduleRules.ExtensionPreviewItem> items) =>
            $"{(x.Mode == ExtensionMode.Group ? "جماعي" : "مستقل")} — {ScheduleRules.DayCountAr(x.DayCount)}\n" +
            string.Join("\n", items.Select(i => $"«{i.TaskName}»: {Format(i.OldEnd)} ← {Format(i.NewEnd)} (+{i.Days})")) +
            $"\nالسبب: {x.Reason}";

        private async Task<ScheduleExtensionResponseDto> ExtensionDetailAsync(Viewer viewer, ScheduleExtension extension)
        {
            // التعارضات والمقترحات والتقادم تهمّ الطلب قبل البتّ فيه فقط
            var live = extension.Status is ExtensionStatus.Draft or ExtensionStatus.Submitted;
            var conflicts = new List<ScheduleConflictDto>();
            var suggested = new List<ScheduleAffectedTaskDto>();
            string? stale = null;
            if (live)
            {
                var schedule = await _schedules.GetByIdAsync(extension.ScheduleId);
                if (schedule != null)
                {
                    var byId = schedule.Tasks.ToDictionary(t => t.Id);
                    var edges = (await _schedules.GetDependenciesAsync(schedule.Id)).Select(d => new ScheduleRules.Edge(d.PredecessorId, d.SuccessorId)).ToList();
                    var preview = extension.Items.Where(i => byId.ContainsKey(i.TaskId)).Select(i => new ScheduleRules.ExtensionPreviewItem(
                        i.TaskId, byId[i.TaskId].Name, i.Days, i.MovesStart, i.OldStart, i.OldEnd, i.NewStart, i.NewEnd)).ToList();
                    conflicts = ToConflicts(ScheduleRules.PreviewConflicts(preview, byId, edges), byId);
                    suggested = ToSuggested(ScheduleRules.SuggestedSuccessors(extension.Items.Select(i => i.TaskId), edges, byId), byId);
                    stale = ScheduleRules.StalePreviewProblem(extension.Items, byId);
                }
            }
            return ToExtensionDto(extension, extension.Project, viewer, stale, conflicts, suggested);
        }

        private static ScheduleExtensionResponseDto ToExtensionDto(ScheduleExtension x, Project project, Viewer viewer, string? stale,
            List<ScheduleConflictDto> conflicts, List<ScheduleAffectedTaskDto> suggested)
        {
            var open = !ProjectRules.IsFinished(project.Stage);
            return new ScheduleExtensionResponseDto
            {
                Id = x.Id, ProjectId = x.ProjectId, ScheduleId = x.ScheduleId, DelayId = x.DelayId,
                Mode = (int)x.Mode, ModeName = x.Mode == ExtensionMode.Group ? "جماعي" : "مستقل",
                DayCount = (int)x.DayCount, DayCountName = ScheduleRules.DayCountAr(x.DayCount),
                Status = (int)x.Status, StatusName = ScheduleRules.ExtensionStatusAr(x.Status), Reason = x.Reason,
                RequestedByUserId = x.RequestedByUserId, RequestedByName = x.RequestedByUser?.FullName ?? string.Empty, RequestedAt = x.RequestedAt,
                DecidedByName = x.DecidedByUser?.FullName, DecidedAt = x.DecidedAt, DecisionNote = x.DecisionNote,
                Items = x.Items.OrderBy(i => i.NewEnd).ThenBy(i => i.Id).Select(i => new ScheduleExtensionItemResponseDto
                {
                    TaskId = i.TaskId, TaskCode = i.Task?.Code ?? string.Empty, TaskName = i.Task?.Name ?? string.Empty, Days = i.Days,
                    MovesStart = i.MovesStart, OldStart = i.OldStart, OldEnd = i.OldEnd, NewStart = i.NewStart, NewEnd = i.NewEnd
                }).ToList(),
                Documents = x.Documents.Select(d => new ScheduleExtensionDocumentDto
                {
                    VersionId = d.DocumentVersionId, DocumentTitle = d.DocumentVersion?.Document?.Title ?? string.Empty,
                    FileName = d.DocumentVersion?.FileName ?? string.Empty, VersionNumber = d.DocumentVersion?.VersionNumber ?? 0,
                    IsApproved = d.DocumentVersion?.IsApproved ?? false
                }).ToList(),
                Conflicts = conflicts, SuggestedTasks = suggested, StaleProblem = stale, RowVersion = x.RowVersion,
                CanEdit = open && x.Status == ExtensionStatus.Draft && x.RequestedByUserId == viewer.Id && viewer.Has(AppPermissions.RequestScheduleExtension),
                CanDecide = open && x.Status == ExtensionStatus.Submitted && x.RequestedByUserId != viewer.Id && viewer.Has(AppPermissions.ApproveScheduleExtension)
            };
        }

        private static ScheduleExtensionItemResponseDto ToItemDto(ScheduleRules.ExtensionPreviewItem p, IReadOnlyDictionary<int, ScheduleTask> byId) => new()
        {
            TaskId = p.TaskId, TaskCode = byId[p.TaskId].Code, TaskName = p.TaskName, Days = p.Days, MovesStart = p.MovesStart,
            OldStart = p.OldStart, OldEnd = p.OldEnd, NewStart = p.NewStart, NewEnd = p.NewEnd
        };

        private static List<ScheduleConflictDto> ToConflicts(IEnumerable<ScheduleRules.Edge> edges, IReadOnlyDictionary<int, ScheduleTask> byId) =>
            edges.Select(e => new ScheduleConflictDto
            {
                PredecessorId = e.PredecessorId, SuccessorId = e.SuccessorId,
                PredecessorName = byId[e.PredecessorId].Name, SuccessorName = byId[e.SuccessorId].Name
            }).ToList();

        private static List<ScheduleAffectedTaskDto> ToSuggested(IEnumerable<int> ids, IReadOnlyDictionary<int, ScheduleTask> byId) =>
            ids.Select(id => new ScheduleAffectedTaskDto { TaskId = id, Code = byId[id].Code, Name = byId[id].Name }).ToList();
    }
}
