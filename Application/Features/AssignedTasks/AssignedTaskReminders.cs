using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.AssignedTasks
{
    /// <summary>
    /// تذكيرات موعد المهام (قرار المستخدم 2026-10-07): إشعار «تستحق اليوم/غداً» مرة واحدة لكل موعد، وإشعار «تأخرت» مرة واحدة.
    /// يصل للمنفِّذين والمُسنِد، وللمُسنِد وحده إن كانت المهمة بانتظار مراجعته (المنفِّذ أنهى دوره). المنجزة لا تُذكَّر.
    /// الأعلام DueSoonNotifiedAt/OverdueNotifiedAt تمنع التكرار، وتُصفَّر عند تغيير تاريخ التسليم.
    /// يُستدعى من العامل الخلفي دورياً، وهو آمن للتكرار (لا يرسل مرتين).
    /// </summary>
    public static class AssignedTaskReminders
    {
        public static async Task<int> RunAsync(
            IAssignedTaskService tasks, IUserPermissionService permissions, INotificationService notifications, DateTime localNow)
        {
            var today = localNow.Date;
            var candidates = await tasks.GetForRemindersAsync(today.AddDays(1));
            var sent = 0;
            var stamp = DateTime.UtcNow;

            foreach (var task in candidates)
            {
                var due = task.DueDate!.Value.Date;
                var overdue = due < today;

                NotificationType type;
                string title, message;
                if (overdue && task.OverdueNotifiedAt == null)
                {
                    type = NotificationType.TaskOverdue;
                    title = "مهمة متأخرة";
                    var days = (today - due).Days;
                    message = $"«{task.Title}» تجاوزت موعد تسليمها ({due:yyyy/MM/dd}) بـ {days} {(days == 1 ? "يوم" : "أيام")}";
                    task.OverdueNotifiedAt = stamp;
                    task.DueSoonNotifiedAt ??= stamp;      // لا تذكير «قريباً» بعد التأخر
                }
                else if (!overdue && task.DueSoonNotifiedAt == null)
                {
                    type = NotificationType.TaskDueSoon;
                    title = "مهمة تقترب من موعدها";
                    message = $"«{task.Title}» {(due == today ? "تستحق اليوم" : "تستحق غداً")} ({due:yyyy/MM/dd})";
                    task.DueSoonNotifiedAt = stamp;
                }
                else continue;

                var recipients = new List<int> { task.CreatedByUserId };
                if (task.Status != AssignedTaskStatus.InReview)
                    recipients.AddRange(await AssignedTaskRules.HandlerUserIdsAsync(permissions, task));

                await AssignedTaskRules.NotifyAsync(notifications, recipients, excludeUserId: -1, task, type, title, message);
                sent++;
            }

            if (candidates.Count > 0) await tasks.SaveChangesAsync();
            return sent;
        }
    }
}
