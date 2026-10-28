using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.Diwan
{
    /// <summary>
    /// تنبيهات مواعيد المعاملات الواردة (قرار المرحلة 2: النظام ينبّه ولا يقرّر): الوارد المفتوح (مسجّلة أو قيد المعالجة) الذي يحين موعده
    /// اليوم أو غداً، أو تجاوزه، يُنبَّه عنه من يعالجه في وحدته الحالية (والموظف المسمّى إن وُجد)، مرة لكل موعد لكل نوع تنبيه.
    /// لا تتغير حالة المعاملة ولا موعدها. إن خلت الوحدة ممن يعالج وصل التنبيه لمن سجّلها كي لا يضيع. تكرار التشغيل آمن.
    /// </summary>
    public static class DiwanReminders
    {
        public const int BatchSize = 500;

        public static async Task<int> RunAsync(IDiwanService diwan, IUserPermissionService permissions, INotificationService notifications, DateTime localNow)
        {
            var today = localNow.Date;
            var candidates = await diwan.GetReminderCandidatesAsync(today, BatchSize);
            var sent = 0;

            foreach (var c in candidates)
            {
                if (DiwanRules.Reminder(c, today) is not DiwanRules.ReminderKind kind) continue;

                var holders = c.CurrentOfficeId is int office
                    ? await permissions.GetUsersWithPermissionAsync(AppPermissions.HandleCorrespondence, officeId: office)
                    : c.CurrentDepartmentId is int department
                        ? await permissions.GetUsersWithPermissionAsync(AppPermissions.HandleCorrespondence, departmentId: department, exactUnit: true)
                        : [];
                var ids = holders.Select(u => u.Id).ToList();
                if (c.CurrentUserId is int named) ids.Add(named);
                if (ids.Count == 0) ids.Add(c.RegisteredByUserId);

                var late = (today - c.DueDate!.Value.Date).Days;
                var overdue = kind == DiwanRules.ReminderKind.Overdue;
                var title = overdue ? "معاملة تجاوزت مهلتها" : "معاملة يقترب موعدها";
                var message = overdue
                    ? $"تجاوزت المعاملة «{c.Subject}» ({c.Type?.Name}) مهلة معالجتها منذ {late} {(late == 1 ? "يوم" : "أيام")}: راجع حالتها أو أحِلها"
                    : $"يحين موعد معالجة المعاملة «{c.Subject}» ({c.Type?.Name}) {(c.DueDate.Value.Date == today ? "اليوم" : "غداً")}";

                var now = DateTime.UtcNow;
                await notifications.AddRangeAsync(ids.Distinct().Select(id => new Notification
                {
                    UserId = id, Title = title, Message = message,
                    Type = overdue ? NotificationType.CorrespondenceOverdue : NotificationType.CorrespondenceDueSoon,
                    RelatedEntityType = "Correspondence", RelatedEntityId = c.Id, CreatedAt = now
                }).ToList());

                await diwan.MarkReminderSentAsync(c.Id, overdue);
                sent++;
            }
            return sent;
        }
    }
}
