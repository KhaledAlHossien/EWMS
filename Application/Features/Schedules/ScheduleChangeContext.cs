using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using static Application.Features.Schedules.ScheduleChangeSupport;

namespace Application.Features.Schedules
{
    /// <summary>
    /// تأخيرات الجدول الزمني وتمديداته
    /// تبعيات المعالجات المشتركة وما تشترك فيه من تحميل وحراسة (تركيب لا وراثة): كل حالة استخدام معالج مستقل يأخذ هذا السياق.
    /// </summary>
    public sealed class ScheduleChangeContext(IScheduleChangeService changes, IScheduleService schedules, IProjectService projects,
            IProjectDocumentService documents, IPublicHolidayService holidays, INotificationService notifications,
            IUserService users, IUserPermissionService permissions)
    {
        public IScheduleChangeService Changes { get; } = changes;
        public IScheduleService Schedules { get; } = schedules;
        public IProjectService Projects { get; } = projects;
        public IProjectDocumentService Documents { get; } = documents;
        public IPublicHolidayService Holidays { get; } = holidays;
        public INotificationService Notifications { get; } = notifications;
        public IUserService Users { get; } = users;
        public IUserPermissionService Permissions { get; } = permissions;

        public Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(Users, Permissions);

        public async Task<Project> LoadProjectAsync(Viewer viewer, int projectId)
        {
            var project = await Projects.GetByIdAsync(projectId) ?? throw new KeyNotFoundException("المشروع غير موجود");
            ProjectRules.EnsureView(viewer, project);
            return project;
        }

        public async Task<ScheduleDelay> LoadDelayAsync(Viewer viewer, int id)
        {
            var delay = await Changes.GetDelayAsync(id) ?? throw new KeyNotFoundException("التأخير غير موجود");
            ProjectRules.EnsureView(viewer, delay.Project);
            return delay;
        }

        public async Task<ScheduleExtension> LoadExtensionAsync(Viewer viewer, int id)
        {
            var extension = await Changes.GetExtensionAsync(id) ?? throw new KeyNotFoundException("طلب التمديد غير موجود");
            ProjectRules.EnsureView(viewer, extension.Project);
            return extension;
        }

        /// <summary>البنود المتأثرة من الجدول نفسه، غير البند المتأخر نفسه، وبلا تكرار</summary>
        public async Task<List<ScheduleTask>> ResolveAffectedAsync(int scheduleId, int delayedTaskId, IReadOnlyCollection<int> ids)
        {
            var wanted = ids.Distinct().ToList();
            if (wanted.Count == 0) return [];
            var schedule = await Schedules.GetByIdAsync(scheduleId) ?? throw new KeyNotFoundException("الجدول غير موجود");
            var byId = schedule.Tasks.ToDictionary(t => t.Id);
            if (wanted.Contains(delayedTaskId)) throw new InvalidOperationException("البند المتأخر نفسه ليس من البنود المتأثرة");
            if (wanted.Any(id => !byId.ContainsKey(id))) throw new InvalidOperationException("بند متأثر لا يتبع هذا الجدول");
            return wanted.Select(id => byId[id]).ToList();
        }

        public async Task<ScheduleDelayResponseDto> DelayDetailAsync(Viewer viewer, ScheduleDelay delay)
        {
            // لواحق مقترحة للمراجعة: لتأخير حيّ على الجدول المعتمد فقط
            List<ScheduleAffectedTaskDto>? suggested = null;
            if (delay.Status != DelayStatus.Resolved && delay.Task.Schedule.Status == ScheduleStatus.Approved)
            {
                var schedule = await Schedules.GetByIdAsync(delay.Task.ScheduleId);
                if (schedule != null)
                {
                    var edges = (await Schedules.GetDependenciesAsync(schedule.Id)).Select(d => new ScheduleRules.Edge(d.PredecessorId, d.SuccessorId));
                    var byId = schedule.Tasks.ToDictionary(t => t.Id);
                    var already = delay.AffectedTasks.Select(a => a.TaskId).ToHashSet();
                    suggested = ScheduleRules.SuggestedSuccessors([delay.TaskId], edges, byId)
                        .Where(id => !already.Contains(id))
                        .Select(id => new ScheduleAffectedTaskDto { TaskId = id, Code = byId[id].Code, Name = byId[id].Name }).ToList();
                }
            }
            return ToDelayDto(delay, delay.Project, viewer, suggested);
        }

        /// <summary>
        /// معاينة سطور الطلب على التواريخ الحالية للجدول المعتمد: تتحقق من السطور (بند مكرر، مكتمل، أيام خارج الحد، جماعي بأيام مختلفة)
        /// وتحسب التواريخ الجديدة بالأيام التقويمية أو بأيام العمل (بالعطل الرسمية).
        /// </summary>
        public async Task<(ProjectSchedule Schedule, Dictionary<int, ScheduleTask> ById, List<ScheduleRules.ExtensionPreviewItem> Preview)> BuildPreviewAsync(
            Project project, ScheduleExtensionRequestDto dto)
        {
            var schedule = await Schedules.GetApprovedAsync(project.Id)
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
                holidays = await Holidays.GetDatesAsync(chosen.Min(t => t.PlannedStart).Date, chosen.Max(t => t.PlannedEnd).Date.AddDays(800));
            }
            return (schedule, byId, ScheduleRules.Preview(lines, byId, count, holidays));
        }

        /// <summary>التأخير المربوط: من هذا المشروع، على الجدول المعتمد الحالي، واعتُمد تبريره</summary>
        public async Task<ScheduleDelay?> ResolveDelayAsync(Project project, ProjectSchedule schedule, int? delayId)
        {
            if (delayId is not int id) return null;
            var delay = await Changes.GetDelayAsync(id) ?? throw new KeyNotFoundException("التأخير غير موجود");
            if (delay.ProjectId != project.Id || delay.Task.ScheduleId != schedule.Id)
                throw new InvalidOperationException("التأخير لا يتبع الجدول المعتمد الحالي لهذا المشروع");
            if (delay.Status != DelayStatus.Approved)
                throw new InvalidOperationException("يُربط بالتمديد تأخير اعتُمد تبريره فقط");
            return delay;
        }

        public async Task<List<ProjectDocumentVersion>> ResolveDocumentsAsync(Project project, IEnumerable<int> ids)
        {
            var result = new List<ProjectDocumentVersion>();
            foreach (var id in ids.Distinct())
            {
                var version = await Documents.GetVersionAsync(id) ?? throw new KeyNotFoundException("مستند داعم غير موجود");
                if (version.Document.ProjectId != project.Id)
                    throw new InvalidOperationException("المستندات الداعمة من وثائق هذا المشروع فقط");
                result.Add(version);
            }
            return result;
        }

        public async Task<ScheduleExtensionResponseDto> ExtensionDetailAsync(Viewer viewer, ScheduleExtension extension)
        {
            // التعارضات والمقترحات والتقادم تهمّ الطلب قبل البتّ فيه فقط
            var live = extension.Status is ExtensionStatus.Draft or ExtensionStatus.Submitted;
            var conflicts = new List<ScheduleConflictDto>();
            var suggested = new List<ScheduleAffectedTaskDto>();
            string? stale = null;
            if (live)
            {
                var schedule = await Schedules.GetByIdAsync(extension.ScheduleId);
                if (schedule != null)
                {
                    var byId = schedule.Tasks.ToDictionary(t => t.Id);
                    var edges = (await Schedules.GetDependenciesAsync(schedule.Id)).Select(d => new ScheduleRules.Edge(d.PredecessorId, d.SuccessorId)).ToList();
                    var preview = extension.Items.Where(i => byId.ContainsKey(i.TaskId)).Select(i => new ScheduleRules.ExtensionPreviewItem(
                        i.TaskId, byId[i.TaskId].Name, i.Days, i.MovesStart, i.OldStart, i.OldEnd, i.NewStart, i.NewEnd)).ToList();
                    conflicts = ToConflicts(ScheduleRules.PreviewConflicts(preview, byId, edges), byId);
                    suggested = ToSuggested(ScheduleRules.SuggestedSuccessors(extension.Items.Select(i => i.TaskId), edges, byId), byId);
                    stale = ScheduleRules.StalePreviewProblem(extension.Items, byId);
                }
            }
            return ToExtensionDto(extension, extension.Project, viewer, stale, conflicts, suggested);
        }
    }
}
