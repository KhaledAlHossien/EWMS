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
    /// <summary>دوال ساكنة مشتركة بين معالجات تأخيرات الجدول الزمني وتمديداته: تحويل إلى DTO وتنسيق وحراسات صرفة.</summary>
    public static class ScheduleChangeSupport
    {
        public static DateTime Today => DateTime.Today;

        public static string Format(DateTime date) => date.ToString("yyyy-MM-dd");

                /// <summary>إصدار التأخير أو التمديد يجب أن يكون هو المعتمد الحالي (التواريخ الحية فيه)</summary>
        public static void EnsureApproved(ProjectSchedule schedule, string what)
        {
            if (schedule.Status != ScheduleStatus.Approved)
                throw new InvalidOperationException($"{what} على إصدار سابق من الجدول — لا يُعالج إلا على الجدول المعتمد الحالي");
        }

        public static ScheduleDelayResponseDto ToDelayDto(ScheduleDelay d, Project project, Viewer viewer, List<ScheduleAffectedTaskDto>? suggested)
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

                /// <summary>طلب في المسودة وصاحبه وحده يعدّله أو يرفعه أو يحذفه (فصل الطلب عن الاعتماد)</summary>
        public static void EnsureRequesterDraft(Viewer viewer, ScheduleExtension extension)
        {
            if (extension.Status != ExtensionStatus.Draft)
                throw new InvalidOperationException("الطلب لم يعد مسودة");
            if (extension.RequestedByUserId != viewer.Id)
                throw new InvalidOperationException("يعدّل طلب التمديد صاحبه وحده");
        }

        public static ScheduleExtensionItem ToItem(ScheduleRules.ExtensionPreviewItem p) => new()
        {
            TaskId = p.TaskId, Days = p.Days, MovesStart = p.MovesStart,
            OldStart = p.OldStart, OldEnd = p.OldEnd, NewStart = p.NewStart, NewEnd = p.NewEnd
        };

        public static string ExtensionSummary(ScheduleExtension x, IEnumerable<ScheduleRules.ExtensionPreviewItem> items) =>
            $"{(x.Mode == ExtensionMode.Group ? "جماعي" : "مستقل")} — {ScheduleRules.DayCountAr(x.DayCount)}\n" +
            string.Join("\n", items.Select(i => $"«{i.TaskName}»: {Format(i.OldEnd)} ← {Format(i.NewEnd)} (+{i.Days})")) +
            $"\nالسبب: {x.Reason}";

        public static ScheduleExtensionResponseDto ToExtensionDto(ScheduleExtension x, Project project, Viewer viewer, string? stale,
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

        public static List<ScheduleConflictDto> ToConflicts(IEnumerable<ScheduleRules.Edge> edges, IReadOnlyDictionary<int, ScheduleTask> byId) =>
            edges.Select(e => new ScheduleConflictDto
            {
                PredecessorId = e.PredecessorId, SuccessorId = e.SuccessorId,
                PredecessorName = byId[e.PredecessorId].Name, SuccessorName = byId[e.SuccessorId].Name
            }).ToList();

        public static List<ScheduleAffectedTaskDto> ToSuggested(IEnumerable<int> ids, IReadOnlyDictionary<int, ScheduleTask> byId) =>
            ids.Select(id => new ScheduleAffectedTaskDto { TaskId = id, Code = byId[id].Code, Name = byId[id].Name }).ToList();
    }
}
