using Application.DTOs.Response;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.AssignedTasks
{
    /// <summary>جدولة المهام الدورية وتحويل القوالب والتكرارات إلى DTO (المصدر الوحيد)</summary>
    public static class TaskPlanningRules
    {
        public const string RecurrenceEntityType = "TaskRecurrence";
        public const int MaxLinksPerTask = 10;
        public const int MaxTemplates = 50;
        public const int MaxRecurrences = 30;

        private static readonly string[] DayNames = ["الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت"];

        /// <summary>أول تاريخ ≥ from يطابق الجدول (التاريخ فقط، بلا وقت)</summary>
        public static DateTime NextRun(TaskRecurrenceFrequency frequency, int? dayOfWeek, int? dayOfMonth, DateTime from)
        {
            var day = from.Date;
            switch (frequency)
            {
                case TaskRecurrenceFrequency.Daily:
                    return day;
                case TaskRecurrenceFrequency.Weekly:
                    var wanted = dayOfWeek ?? 0;
                    return day.AddDays(((wanted - (int)day.DayOfWeek) + 7) % 7);
                default:
                    var dom = dayOfMonth ?? 1;
                    var candidate = MonthDay(day.Year, day.Month, dom);
                    if (candidate >= day) return candidate;
                    var next = day.AddMonths(1);
                    return MonthDay(next.Year, next.Month, dom);
            }
        }

        private static DateTime MonthDay(int year, int month, int day) =>
            new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));

        /// <summary>التاريخ التالي بعد تنفيذ اليوم، أو null إن انتهت المدة</summary>
        public static DateTime? AdvanceAfter(AssignedTaskRecurrence r, DateTime today)
        {
            var next = NextRun(r.Frequency, r.DayOfWeek, r.DayOfMonth, today.AddDays(1));
            return r.EndDate != null && next > r.EndDate.Value.Date ? null : next;
        }

        public static string ScheduleAr(AssignedTaskRecurrence r) => r.Frequency switch
        {
            TaskRecurrenceFrequency.Daily => "كل يوم",
            TaskRecurrenceFrequency.Weekly => $"كل أسبوع يوم {DayNames[Math.Clamp(r.DayOfWeek ?? 0, 0, 6)]}",
            _ => $"كل شهر في اليوم {r.DayOfMonth ?? 1}"
        };

        public static TaskTemplateDto ToDto(AssignedTaskTemplate t) => new()
        {
            Id = t.Id,
            Name = t.Name,
            Title = t.Title,
            Description = t.Description,
            Priority = t.Priority.ToString(),
            PriorityAr = AssignedTaskRules.PriorityAr(t.Priority),
            DefaultDueDays = t.DefaultDueDays,
            Items = t.Items.OrderBy(i => i.SortOrder).Select(i => i.Text).ToList()
        };

        public static TaskRecurrenceDto ToDto(AssignedTaskRecurrence r, string targetName) => new()
        {
            Id = r.Id,
            TemplateId = r.TemplateId,
            TemplateName = r.Template.Name,
            TaskTitle = r.Template.Title,
            TargetType = r.TargetType.ToString(),
            TargetTypeAr = AssignedTaskRules.TargetTypeLabel(r.TargetType),
            TargetId = r.TargetId,
            TargetName = targetName,
            Frequency = (int)r.Frequency,
            ScheduleAr = ScheduleAr(r),
            DayOfWeek = r.DayOfWeek,
            DayOfMonth = r.DayOfMonth,
            DueAfterDays = r.DueAfterDays,
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            IsActive = r.IsActive,
            NextRunDate = r.NextRunDate,
            LastRunAt = r.LastRunAt,
            LastTaskId = r.LastTaskId,
            LastError = r.LastError
        };
    }
}
