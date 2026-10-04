using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Maintenance;
using Domain.Enums;

namespace Application.Features.Maintenance
{
    /// <summary>
    /// إشعارات الصيانة (نفس نمط VacationNotifier):
    /// طلب جديد ← من يملك نقل طلبات القسم | نقل إلى موظف آخر ← الموظف الجديد والسابق |
    /// تغيّر الحالة/تعديل: من الفني ← من يملك نقل طلبات القسم، ومن غيره ← الفني.
    /// من قام بالإجراء لا يصله إشعار عن إجرائه.
    /// </summary>
    public static class MaintenanceNotifier
    {
        private static async Task<List<int>> ManagerIdsAsync(IUserPermissionService permissions, int? departmentId)
        {
            if (departmentId is not int id) return [];

            // من يدير طلبات القسم: موظفو القسم الذين يملك دورهم نقل طلباته (AssignMaintenanceRequest)
            return (await permissions.GetUsersWithPermissionAsync("AssignMaintenanceRequest", departmentId: id))
                .Select(u => u.Id)
                .ToList();
        }

        private static async Task SendAsync(
            INotificationService notifications, IEnumerable<int> userIds, int actorId,
            NotificationType type, string entityType, int entityId, string title, string message)
        {
            var rows = userIds.Distinct().Where(id => id != actorId).Select(id => new Notification
            {
                UserId = id,
                Title = title,
                Message = message,
                Type = type,
                RelatedEntityType = entityType,
                RelatedEntityId = entityId,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            if (rows.Count > 0)
                await notifications.AddRangeAsync(rows);
        }

        private static string Label(MaintenanceRequest r) =>
            $"{MaintenanceRules.RequestNumber(r.Id, r.CreatedAt)} ({r.ClientName})";

        public static async Task RequestCreatedAsync(
            INotificationService notifications, IUserPermissionService permissions, MaintenanceRequest request, User actor)
        {
            await SendAsync(notifications, await ManagerIdsAsync(permissions, request.DepartmentId), actor.Id,
                NotificationType.MaintenanceRequestCreated, MaintenanceRules.RequestEntity, request.Id,
                "طلب صيانة جديد",
                $"سجّل {actor.FullName} طلب الصيانة {Label(request)}");
        }

        /// <summary>تغيّر الحالة أو تعديل البيانات: الفني يُبلَّغ إن لم يكن هو الفاعل، وإلا يُبلَّغ رئيس القسم</summary>
        public static async Task RequestChangedAsync(
            INotificationService notifications, IUserPermissionService permissions, MaintenanceRequest request, User actor, string what)
        {
            var recipients = actor.Id == request.UserId
                ? await ManagerIdsAsync(permissions, request.DepartmentId)
                : [request.UserId];

            await SendAsync(notifications, recipients, actor.Id,
                NotificationType.MaintenanceStatusChanged, MaintenanceRules.RequestEntity, request.Id,
                "تحديث على طلب صيانة",
                $"{actor.FullName}: {what} — الطلب {Label(request)}");
        }

        public static async Task RequestReassignedAsync(
            INotificationService notifications, MaintenanceRequest request, User actor, int previousUserId, User newUser)
        {
            await SendAsync(notifications, [newUser.Id], actor.Id,
                NotificationType.MaintenanceAssigned, MaintenanceRules.RequestEntity, request.Id,
                "أُسند إليك طلب صيانة",
                $"أسند إليك {actor.FullName} طلب الصيانة {Label(request)}");

            await SendAsync(notifications, [previousUserId], actor.Id,
                NotificationType.MaintenanceAssigned, MaintenanceRules.RequestEntity, request.Id,
                "نُقل طلب صيانة عنك",
                $"نقل {actor.FullName} طلب الصيانة {Label(request)} إلى {newUser.FullName}");
        }

        /// <summary>مهمة جديدة وجّهها رئيس القسم إلى موظف</summary>
        public static async Task TaskAssignedAsync(INotificationService notifications, MaintenanceTask task, User actor)
        {
            await SendAsync(notifications, [task.UserId], actor.Id,
                NotificationType.MaintenanceAssigned, MaintenanceRules.TaskEntity, task.Id,
                "مهمة صيانة جديدة",
                $"وجّه إليك {actor.FullName} مهمة صيانة في {task.TaskLocation} — الجهة الطالبة: {task.RequestingParty}");
        }

        public static async Task TaskReassignedAsync(
            INotificationService notifications, MaintenanceTask task, User actor, int previousUserId, User newUser)
        {
            await SendAsync(notifications, [newUser.Id], actor.Id,
                NotificationType.MaintenanceAssigned, MaintenanceRules.TaskEntity, task.Id,
                "أُسندت إليك مهمة صيانة",
                $"أسند إليك {actor.FullName} مهمة الصيانة في {task.TaskLocation}");

            await SendAsync(notifications, [previousUserId], actor.Id,
                NotificationType.MaintenanceAssigned, MaintenanceRules.TaskEntity, task.Id,
                "نُقلت مهمة صيانة عنك",
                $"نقل {actor.FullName} مهمة الصيانة في {task.TaskLocation} إلى {newUser.FullName}");
        }
    }
}
