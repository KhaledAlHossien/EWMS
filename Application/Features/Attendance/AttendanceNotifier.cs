using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.Attendance
{
    /// <summary>
    /// إشعارات أجهزة البصمة (نفس نمط VacationNotifier): جهاز جديد → من يقبل الأجهزة؛ انقطاع وعودة وتغيّر عنوان →
    /// من يرى كل الأجهزة، ومن يرى أجهزة منطقة الجهاز (فرعه في تلك المنطقة).
    /// </summary>
    public static class AttendanceNotifier
    {
        public const string EntityType = "AttendanceDevice";

        public static async Task DevicePendingAsync(IUserPermissionService permissions, INotificationService notifications,
            int deviceId, string serialNumber, string? ip)
        {
            var recipients = await permissions.GetUsersWithPermissionAsync(AppPermissions.ApproveAttendanceDevice);
            await SendAsync(notifications, recipients, NotificationType.AttendanceDevicePending, deviceId,
                "جهاز بصمة جديد بانتظار الموافقة",
                $"اتصل الجهاز {serialNumber}{(ip == null ? "" : $" من العنوان {ip}")}. اقبله وحدد منطقته من صفحة أجهزة البصمة، أو ارفضه إن لم يكن من أجهزتكم.");
        }

        public static Task DeviceOfflineAsync(IUserPermissionService permissions, INotificationService notifications, IAttendanceAreaService areas,
            int deviceId, string name, int? areaId, int minutes) =>
            AreaAsync(permissions, notifications, areas, areaId, NotificationType.AttendanceDeviceOffline, deviceId,
                $"انقطع جهاز البصمة «{name}»", $"لم يتصل الجهاز «{name}» بالخادم منذ {minutes} دقيقة على الأقل. تحقق من الكهرباء والشبكة.");

        public static Task DeviceOnlineAsync(IUserPermissionService permissions, INotificationService notifications, IAttendanceAreaService areas,
            int deviceId, string name, int? areaId) =>
            AreaAsync(permissions, notifications, areas, areaId, NotificationType.AttendanceDeviceOnline, deviceId,
                $"عاد جهاز البصمة «{name}» للاتصال", $"عاد الجهاز «{name}» للاتصال بالخادم، وما سُجّل عليه أثناء الانقطاع يصل تلقائياً.");

        public static Task DeviceIpChangedAsync(IUserPermissionService permissions, INotificationService notifications, IAttendanceAreaService areas,
            int deviceId, string name, int? areaId, string from, string? to) =>
            AreaAsync(permissions, notifications, areas, areaId, NotificationType.AttendanceDeviceIpChanged, deviceId,
                $"تغيّر عنوان جهاز البصمة «{name}»", $"يتصل الجهاز «{name}» الآن من العنوان {to} بدل {from}. إن لم يكن تغييراً معروفاً في الشبكة فتحقق منه.");

        private static async Task AreaAsync(IUserPermissionService permissions, INotificationService notifications, IAttendanceAreaService areas,
            int? areaId, NotificationType type, int deviceId, string title, string message)
        {
            var recipients = await permissions.GetUsersWithPermissionAsync(AppPermissions.ViewAttendanceDevices);
            if (areaId != null)
            {
                var branchesInArea = (await areas.GetBranchesAsync()).Where(b => b.AttendanceAreaId == areaId).Select(b => b.Id).ToHashSet();
                recipients.AddRange((await permissions.GetUsersWithPermissionAsync(AppPermissions.ViewAreaAttendanceDevices))
                    .Where(u => u.BranchId is int b && branchesInArea.Contains(b)));
            }
            await SendAsync(notifications, recipients, type, deviceId, title, message);
        }

        private static async Task SendAsync(INotificationService notifications, IEnumerable<User> recipients, NotificationType type,
            int deviceId, string title, string message)
        {
            var now = DateTime.UtcNow;
            var list = recipients.DistinctBy(u => u.Id).Select(u => new Notification
            {
                UserId = u.Id, Title = title, Message = message, Type = type,
                RelatedEntityType = EntityType, RelatedEntityId = deviceId, CreatedAt = now
            }).ToList();
            if (list.Count > 0) await notifications.AddRangeAsync(list);
        }
    }
}
