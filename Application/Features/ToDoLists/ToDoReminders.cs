using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.ToDoLists
{
    /// <summary>
    /// تذكيرات بنود «مفكرتي» (قرار المستخدم 2026-10-08): إشعار «يستحق اليوم/غداً» مرة واحدة لكل موعد، وإشعار «تأخر» مرة واحدة.
    /// لصاحب القائمة وحده، والمنجزة وقوائم الأرشيف لا تُذكَّر. الأعلام تُصفَّر عند تغيير الموعد أو تقدّم البند المتكرر.
    /// يستدعيه العامل الخلفي دورياً مع تذكيرات لوحة المهام، وهو آمن للتكرار.
    /// </summary>
    public static class ToDoReminders
    {
        public const string RelatedEntityType = "ToDoList";

        public static async Task<int> RunAsync(IToDoListService lists, INotificationService notifications, DateTime localNow)
        {
            var today = localNow.Date;
            var candidates = await lists.GetForRemindersAsync(today.AddDays(1));
            var stamp = DateTime.UtcNow;
            var sent = 0;

            foreach (var item in candidates)
            {
                var due = item.DueDate!.Value.Date;
                NotificationType type;
                string title, message;
                if (due < today && item.OverdueNotifiedAt == null)
                {
                    type = NotificationType.ToDoItemOverdue;
                    title = "بند متأخر في مفكرتي";
                    var days = (today - due).Days;
                    message = $"«{item.Title}» في «{item.ToDoList.Name}» تجاوز موعده ({due:yyyy/MM/dd}) بـ {days} {(days == 1 ? "يوم" : "أيام")}";
                    item.OverdueNotifiedAt = stamp;
                    item.DueSoonNotifiedAt ??= stamp;
                }
                else if (due >= today && item.DueSoonNotifiedAt == null)
                {
                    type = NotificationType.ToDoItemDueSoon;
                    title = "بند يستحق قريباً في مفكرتي";
                    message = $"«{item.Title}» في «{item.ToDoList.Name}» {(due == today ? "يستحق اليوم" : "يستحق غداً")} ({due:yyyy/MM/dd})";
                    item.DueSoonNotifiedAt = stamp;
                }
                else continue;

                await notifications.AddAsync(new Notification
                {
                    UserId = item.ToDoList.UserId,
                    Title = title,
                    Message = message,
                    Type = type,
                    RelatedEntityType = RelatedEntityType,
                    RelatedEntityId = item.ToDoListId,
                    CreatedAt = DateTime.UtcNow
                });
                sent++;
            }

            if (candidates.Count > 0) await lists.SaveChangesAsync();
            return sent;
        }
    }
}
