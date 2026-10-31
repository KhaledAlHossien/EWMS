using Application.Common;
using Application.Features.Schedules;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.Handover
{
    /// <summary>
    /// تنبيه تجاوز موعد معالجة ملاحظة الاستلام (النظام ينبّه ولا يقرر): ملاحظة لم تُغلق وتجاوزت موعدها يُنبَّه عنها مسؤولُها (إن كان موظفاً)
    /// وحاملو صلاحية متابعة الملاحظات ممن يرون المشروع، مرة لكل موعد (تغيير الموعد يسمح بتنبيه جديد). لا تتغير حالة الملاحظة. تكرار التشغيل آمن.
    /// </summary>
    public static class HandoverReminders
    {
        public const int BatchSize = 500;

        public static async Task<int> RunAsync(IHandoverService handovers, IUserPermissionService permissions, INotificationService notifications, DateTime localNow)
        {
            var today = localNow.Date;
            var candidates = await handovers.GetOverdueNotesAsync(today, BatchSize);
            var granted = new Dictionary<int, IReadOnlySet<string>>();
            var sent = 0;

            foreach (var n in candidates)
            {
                if (n.DueDate is not DateTime due) continue;
                var record = n.HandoverRecord;
                var ids = new List<int>();
                if (n.ResponsibleUserId is int responsible) ids.Add(responsible);
                ids.AddRange(await ScheduleNotifier.HoldersAsync(permissions, record.Project, AppPermissions.FollowHandoverNotes, granted));

                var late = (today - due.Date).Days;
                var now = DateTime.UtcNow;
                await notifications.AddRangeAsync(ids.Distinct().Select(id => new Notification
                {
                    UserId = id,
                    Title = "ملاحظة استلام تجاوزت موعد معالجتها",
                    Message = $"تجاوزت «{HandoverSupport.Shorten(n.Description)}» في {HandoverContext.Label(record)} (المشروع «{record.Project.Name}») موعد معالجتها منذ {late} {(late == 1 ? "يوم" : "أيام")}: راجع حالتها",
                    Type = NotificationType.HandoverNoteOverdue,
                    RelatedEntityType = AuditEntities.Handover,
                    RelatedEntityId = record.Id,
                    CreatedAt = now
                }).ToList());

                await handovers.MarkNoteOverdueNotifiedAsync(n.Id, due);
                sent++;
            }
            return sent;
        }
    }
}
