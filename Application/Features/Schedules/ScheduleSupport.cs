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
    /// <summary>دوال ساكنة مشتركة بين معالجات الجدول الزمني للمشروع: تحويل إلى DTO وتنسيق وحراسات صرفة.</summary>
    public static class ScheduleSupport
    {
        public static DateTime Today => DateTime.Today;

                /// <summary>البنود والتبعيات تُحرَّر في المسودة فقط، وفي مشروع مفتوح</summary>
        public static void EnsureDraftEditable(ProjectSchedule schedule)
        {
            ProjectRules.EnsureOpen(schedule.Project);
            if (schedule.Status != ScheduleStatus.Draft)
                throw new InvalidOperationException(schedule.Status == ScheduleStatus.Approved
                    ? "الجدول المعتمد لا تُعدَّل بنوده مباشرة: التواريخ تتغير بتمديد معتمد، ولإعادة الترتيب أنشئ مسودة جديدة"
                    : "هذا الإصدار سابق ومحفوظ للمراجعة فقط");
        }

                /// <summary>مسؤول البند من مشاركي المشروع</summary>
        public static User? AssigneeOrNull(Project project, int? userId)
        {
            if (userId is not int id) return null;
            return project.Members.FirstOrDefault(m => m.UserId == id)?.User
                ?? throw new InvalidOperationException("مسؤول البند يُختار من مشاركي المشروع — أضفه مشاركاً أولاً");
        }

        public static string Format(DateTime date) => date.ToString("yyyy-MM-dd");

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

        public static StartOrderResponseDto ToStartOrderDto(ProjectStartOrder o) => new()
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
