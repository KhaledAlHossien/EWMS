using Application.Common;
using Application.DTOs.Request;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;

namespace Application.Features.AssignedTasks.Queries.Export
{
    public class ExportAssignedTasksQueryHandler : IRequestHandler<ExportAssignedTasksQuery, byte[]>
    {
        private readonly IAssignedTaskService _taskService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly IAssignedTaskSpreadsheet _spreadsheet;
        private readonly IReportLogService _reportLog;

        public ExportAssignedTasksQueryHandler(IAssignedTaskService taskService, IUserService userService, IUserPermissionService permissions, IAssignedTaskSpreadsheet spreadsheet, IReportLogService reportLog)
        {
            _taskService = taskService;
            _userService = userService;
            _permissions = permissions;
            _spreadsheet = spreadsheet;
            _reportLog = reportLog;
        }

        public async Task<byte[]> Handle(ExportAssignedTasksQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var f = request.Filter;
            var doneSince = DateTime.UtcNow.AddDays(-Math.Clamp(f.DoneDays ?? 30, 1, 3650));
            var cards = (await _taskService.GetBoardAsync(AssignedTaskRules.ModeFilter(viewer, f.Mode)))
                .Where(t => t.Status != AssignedTaskStatus.Done || (t.CompletedAt ?? t.UpdatedAt) >= doneSince)
                .Select(t => AssignedTaskRules.ToCard(t, viewer))
                .Where(c => AssignedTaskRules.MatchesExportFilter(c, f))
                .ToList();
            var content = _spreadsheet.Export(cards);
            await _reportLog.AddAsync(Reports.ReportRules.Log(viewer, Reports.ReportRules.TasksKey, Reports.ReportRules.TasksTitle,
                ReportFormat.Excel, ExportFiltersText(f), cards.Count));
            return content;
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_userService, _permissions);

        /// <summary>نص فلاتر تصدير اللوحة لسجل التقارير</summary>
        private static string ExportFiltersText(AssignedTaskExportFilterDto f)
        {
            var parts = new List<string>
            {
                "العرض: " + ((f.Mode ?? "incoming").ToLowerInvariant() switch { "outgoing" => "الصادرة مني", "scope" => "ضمن نطاقي", _ => "الواردة إليّ" }),
                $"المنجزة: آخر {Math.Clamp(f.DoneDays ?? 30, 1, 3650)} يوماً"
            };
            if (!string.IsNullOrWhiteSpace(f.Q)) parts.Add($"بحث: {f.Q.Trim()}");
            if (Enum.TryParse<AssignedTaskPriority>(f.Priority, true, out var priority)) parts.Add($"الأولوية: {AssignedTaskRules.PriorityAr(priority)}");
            if (f.OverdueOnly) parts.Add("المتأخرة فقط");
            if (f.DueFrom != null) parts.Add($"الاستحقاق من: {f.DueFrom:yyyy-MM-dd}");
            if (f.DueTo != null) parts.Add($"الاستحقاق إلى: {f.DueTo:yyyy-MM-dd}");
            return string.Join(" · ", parts);
        }
    }
}
