using Application.Common;
using Application.DTOs.Response;
using Application.Features.AssignedTasks;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.ToDoLists
{
    /// <summary>قواعد «مفكرتي» المشتركة: ألوان القوائم، التكرار، وربط البند بمهمة من لوحة المهام</summary>
    public static class ToDoRules
    {
        public const int MaxItemsPerList = 200;
        public const int MaxTitleLength = 200;
        public const int MaxNoteLength = 1000;
        public const int MaxBulkItems = 100;
        public const int MaxIconLength = 8;

        public static readonly string[] Colors = ["green", "blue", "purple", "orange", "red", "gray"];

        public static string RepeatAr(TaskRecurrenceFrequency r) => r switch
        {
            TaskRecurrenceFrequency.Daily => "يومياً",
            TaskRecurrenceFrequency.Weekly => "أسبوعياً",
            _ => "شهرياً"
        };

        public static TaskRecurrenceFrequency? ParseRepeat(int? value) =>
            value is null or 0 ? null
            : Enum.IsDefined(typeof(TaskRecurrenceFrequency), value.Value) ? (TaskRecurrenceFrequency)value.Value
            : throw new ArgumentException("نوع التكرار غير صحيح");

        /// <summary>
        /// موعد البند المتكرر بعد إنجازه: يتقدّم من موعده الحالي بخطوات التكرار حتى يصير بعد اليوم
        /// (الإنجاز المتأخر لا يُنتج مواعيد ماضية). الشهري يُقصّ لآخر يوم في الشهر القصير.
        /// </summary>
        public static DateTime NextDue(TaskRecurrenceFrequency repeat, DateTime due, DateTime today)
        {
            var anchorDay = due.Day;
            var next = due.Date;
            do
            {
                next = repeat switch
                {
                    TaskRecurrenceFrequency.Daily => next.AddDays(1),
                    TaskRecurrenceFrequency.Weekly => next.AddDays(7),
                    _ => AddMonthKeepingDay(next, anchorDay)
                };
            } while (next <= today.Date);
            return next;
        }

        private static DateTime AddMonthKeepingDay(DateTime from, int day)
        {
            var m = new DateTime(from.Year, from.Month, 1).AddMonths(1);
            return new DateTime(m.Year, m.Month, Math.Min(day, DateTime.DaysInMonth(m.Year, m.Month)));
        }

        public static bool IsOverdue(ToDoItem i, DateTime today) => !i.IsDone && i.DueDate != null && i.DueDate.Value.Date < today.Date;

        /// <summary>
        /// يملأ المهمة المرتبطة في DTO البنود: من لا يحق له عرض المهمة (فقد وصوله) يرى Available=false بلا بياناتها.
        /// يتطلب أن تكون LinkedTask محمَّلة في الكيانات.
        /// </summary>
        public static void FillLinks(ToDoList entity, ToDoListResponseDto dto, Viewer viewer)
        {
            var today = DateTime.Today;
            foreach (var item in dto.Items)
            {
                var task = entity.Items.FirstOrDefault(i => i.Id == item.Id)?.LinkedTask;
                if (task == null) continue;
                item.LinkedTask = LinkInfo(task, viewer, today);
            }
        }

        public static ToDoLinkedTaskDto LinkInfo(AssignedTask task, Viewer viewer, DateTime today) =>
            AssignedTaskRules.CanView(task, viewer)
                ? new ToDoLinkedTaskDto
                {
                    Id = task.Id, Available = true, Title = task.Title,
                    Status = task.Status.ToString(), StatusAr = AssignedTaskRules.StatusAr(task.Status),
                    IsOverdue = task.Status != AssignedTaskStatus.Done && task.DueDate != null && task.DueDate.Value.Date < today.Date
                }
                : new ToDoLinkedTaskDto { Id = task.Id, Available = false };
    }
}
