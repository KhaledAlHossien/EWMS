using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.AssignedTasks.Queries.GetStats
{
    public class GetTaskStatsQueryHandler : IRequestHandler<GetTaskStatsQuery, TaskStatsDto>
    {
        private readonly IAssignedTaskPlanningService _planning;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public GetTaskStatsQueryHandler(IAssignedTaskPlanningService planning, IUserService userService, IUserPermissionService permissions)
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
