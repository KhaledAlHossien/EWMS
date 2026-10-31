using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.Pricing
{
    /// <summary>
    /// تنبيه تجاوز موعد طلب التسعير (النظام ينبّه ولا يقرر): طلب لم ينتهِ (مسجَّل أو قيد الجمع أو بانتظار الاعتماد) وتجاوز موعده يُنبَّه عنه
    /// مقدّمه والمكلَّف به وحاملو صلاحية «التكليف» في فرعه، مرة لكل موعد (تغيير الموعد يسمح بتنبيه جديد). لا تتغير حالة الطلب. تكرار التشغيل آمن.
    /// </summary>
    public static class PricingReminders
    {
        public const int BatchSize = 500;

        public static async Task<int> RunAsync(IPricingService pricing, IUserPermissionService permissions, INotificationService notifications, DateTime localNow)
        {
            var today = localNow.Date;
            var candidates = await pricing.GetOverdueCandidatesAsync(today, BatchSize);
            var managers = new Dictionary<int, List<int>>();
            var sent = 0;

            foreach (var r in candidates)
            {
                if (r.DueDate is not DateTime due) continue;
                if (!managers.TryGetValue(r.BranchId, out var holders))
                    managers[r.BranchId] = holders = (await permissions.GetUsersWithPermissionAsync(AppPermissions.AssignPricingRequest, r.BranchId)).Select(u => u.Id).ToList();

                var ids = new List<int> { r.RegisteredByUserId };
                if (r.AssigneeUserId is int assignee) ids.Add(assignee);
                ids.AddRange(holders);

                var late = (today - due.Date).Days;
                var now = DateTime.UtcNow;
                await notifications.AddRangeAsync(ids.Distinct().Select(id => new Notification
                {
                    UserId = id,
                    Title = "طلب تسعير تجاوز موعده",
                    Message = $"تجاوز الطلب {PricingRules.Number(r.Id, r.CreatedAt)} — {r.Title} موعده منذ {late} {(late == 1 ? "يوم" : "أيام")} وهو «{PricingRules.StatusAr(r.Status)}»: راجع حالته",
                    Type = NotificationType.PricingRequestOverdue,
                    RelatedEntityType = AuditEntities.PricingRequest,
                    RelatedEntityId = r.Id,
                    CreatedAt = now
                }).ToList());

                await pricing.MarkOverdueNotifiedAsync(r.Id, due);
                sent++;
            }
            return sent;
        }
    }
}
