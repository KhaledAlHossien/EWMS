using Application.Common;
using Application.Features.Schedules;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.Contracts
{
    /// <summary>
    /// تنبيه قرب انتهاء الضمان (قرار المرحلة 4: النظام ينبّه ولا يقرر): عقد ساري أو منجز تنتهي مدة ضمانه خلال النافذة يُنبَّه عنه من أنشأه وحاملو
    /// صلاحية تعديل العقود ممن يرون مشروعه، مرة لكل نهاية ضمان (تغيير النهاية يسمح بتنبيه جديد). لا يتغير شيء في العقد ولا في المحتجز.
    /// تكرار التشغيل آمن.
    /// </summary>
    public static class ContractReminders
    {
        public const int BatchSize = 200;

        public static async Task<int> RunAsync(IContractService contracts, IUserPermissionService permissions, INotificationService notifications, DateTime localNow)
        {
            var today = localNow.Date;
            var candidates = await contracts.GetWarrantyCandidatesAsync(today, ContractContext.WarrantyWindowDays, BatchSize);
            var granted = new Dictionary<int, IReadOnlySet<string>>();
            var sent = 0;

            foreach (var c in candidates)
            {
                if (c.WarrantyEnd is not DateTime end) continue;
                var ids = new List<int> { c.CreatedByUserId };
                ids.AddRange(await ScheduleNotifier.HoldersAsync(permissions, c.Project, AppPermissions.EditContract, granted));

                var left = (end.Date - today).Days;
                var now = DateTime.UtcNow;
                await notifications.AddRangeAsync(ids.Distinct().Select(id => new Notification
                {
                    UserId = id,
                    Title = "ضمان عقد يقترب من نهايته",
                    Message = $"ينتهي ضمان العقد {ContractContext.Label(c)} في المشروع «{c.Project.Name}» في {end:yyyy/MM/dd}" +
                              (left == 0 ? " (اليوم)" : $" (بعد {left} {(left == 1 ? "يوم" : "أيام")})") + ": راجع التزامات الضمان والمبالغ المحتجزة",
                    Type = NotificationType.ContractWarrantyEnding,
                    RelatedEntityType = AuditEntities.Contract,
                    RelatedEntityId = c.Id,
                    CreatedAt = now
                }).ToList());

                await contracts.MarkWarrantyNotifiedAsync(c.Id, end);
                sent++;
            }
            return sent;
        }
    }
}
