using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.Studies
{
    /// <summary>
    /// تنبيه تجاوز موعد مخرج الدراسة (قرار المرحلة 3: النظام ينبّه ولا يقرّر): مخرج لم يُعتمد وتجاوز موعده في أمر مفتوح يُنبَّه عنه
    /// مسؤولُه ومن فتح الأمر، مرة لكل موعد (تغيير الموعد يسمح بتنبيه جديد). لا تتغير حالة المخرج ولا الأمر. تكرار التشغيل آمن.
    /// </summary>
    public static class StudyReminders
    {
        public const int BatchSize = 500;

        public static async Task<int> RunAsync(IStudyService studies, INotificationService notifications, DateTime localNow)
        {
            var today = localNow.Date;
            var candidates = await studies.GetOverdueCandidatesAsync(today, BatchSize);
            var sent = 0;

            foreach (var c in candidates)
            {
                var ids = new List<int> { c.CreatedByUserId };
                if (c.AssigneeUserId is int assignee) ids.Add(assignee);

                var late = (today - c.DueDate.Date).Days;
                var now = DateTime.UtcNow;
                await notifications.AddRangeAsync(ids.Distinct().Select(id => new Notification
                {
                    UserId = id,
                    Title = "مخرج دراسة تجاوز موعده",
                    Message = $"تجاوز المخرج «{c.TypeName}» في أمر الدراسة «{c.StudyTitle}» موعده منذ {late} {(late == 1 ? "يوم" : "أيام")}: راجع حالته",
                    Type = NotificationType.StudyDeliverableOverdue,
                    RelatedEntityType = AuditEntities.Study,
                    RelatedEntityId = c.StudyOrderId,
                    CreatedAt = now
                }).ToList());

                await studies.MarkOverdueNotifiedAsync(c.DeliverableId, c.DueDate);
                sent++;
            }
            return sent;
        }
    }
}
