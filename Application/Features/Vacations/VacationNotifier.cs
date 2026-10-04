using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.Vacations
{
    /// <summary>
    /// إشعارات سير عمل الإجازة. المستلمون حسب الصلاحية لا المنصب (Role-Permission، 2026-10-03):
    /// أصحاب ApproveVacationFirst / ApproveVacationFinal في فرع الإجازة، ومن وافق في المرحلة الأولى، والموظف صاحب الطلب.
    /// </summary>
    internal static class VacationNotifier
    {
        public static async Task NotifySubmittedAsync(
            IUserPermissionService permissions,
            INotificationService notificationService,
            Vacation vacation,
            string employeeFullName)
        {
            // من يملك صلاحية المرحلة الأولى تبدأ إجازته مباشرة بانتظار الاعتماد النهائي
            var toFinal = vacation.Status == VacationStatus.PendingBranchManager;

            var recipients = await ApproversAsync(permissions,
                toFinal ? AppPermissions.ApproveVacationFinal : AppPermissions.ApproveVacationFirst,
                vacation, vacation.UserId);

            await notificationService.AddRangeAsync(recipients.Select(id => New(id, vacation,
                toFinal ? "طلب إجازة بانتظار اعتمادك" : "طلب إجازة جديد",
                toFinal
                    ? $"قدّم {employeeFullName} طلب إجازة ({vacation.VacDayCount} يوم) بحاجة لاعتمادك النهائي"
                    : $"قدّم {employeeFullName} طلب إجازة ({vacation.VacDayCount} يوم) بحاجة لموافقتك",
                NotificationType.VacationSubmitted)));
        }

        public static async Task NotifyApprovedByManagerAsync(
            IUserPermissionService permissions,
            INotificationService notificationService,
            Vacation vacation,
            string employeeFullName)
        {
            var toEmployee = New(vacation.UserId, vacation, "تمت الموافقة على طلبك",
                "تمت الموافقة الأولى على طلب إجازتك، وهو الآن بانتظار الاعتماد النهائي",
                NotificationType.VacationApprovedByManager);

            var finalApprovers = (await ApproversAsync(permissions, AppPermissions.ApproveVacationFinal, vacation, vacation.UserId))
                .Where(id => id != vacation.FirstApprovedByUserId);

            var toApprovers = finalApprovers.Select(id => New(id, vacation, "طلب إجازة بانتظار اعتمادك",
                $"طلب إجازة من {employeeFullName} تمت الموافقة الأولى عليه، وهو الآن بانتظار اعتمادك النهائي",
                NotificationType.VacationForwardedToBranchManager));

            await notificationService.AddRangeAsync(toApprovers.Append(toEmployee));
        }

        public static async Task NotifyRejectedByManagerAsync(
            INotificationService notificationService,
            Vacation vacation,
            string? reason)
        {
            await notificationService.AddAsync(New(vacation.UserId, vacation, "تم رفض طلب إجازتك",
                string.IsNullOrWhiteSpace(reason)
                    ? "رُفض طلب إجازتك في مرحلة الموافقة الأولى"
                    : $"رُفض طلب إجازتك في مرحلة الموافقة الأولى. السبب: {reason}",
                NotificationType.VacationRejectedByManager));
        }

        public static async Task NotifyApprovedFinalAsync(
            INotificationService notificationService,
            Vacation vacation,
            string employeeFullName)
        {
            var toEmployee = New(vacation.UserId, vacation, "تم اعتماد إجازتك نهائياً",
                "تم اعتماد طلب إجازتك نهائياً", NotificationType.VacationApprovedFinal);

            var toFirstApprover = FirstApprover(vacation).Select(id => New(id, vacation, "اعتماد نهائي لإجازة",
                $"اعتُمدت نهائياً إجازة {employeeFullName} التي سبق ووافقت عليها", NotificationType.VacationApprovedFinal));

            await notificationService.AddRangeAsync(toFirstApprover.Append(toEmployee));
        }

        public static async Task NotifyRejectedByBranchManagerAsync(
            INotificationService notificationService,
            Vacation vacation,
            string employeeFullName,
            string? reason)
        {
            var reasonSuffix = string.IsNullOrWhiteSpace(reason) ? "" : $" السبب: {reason}";

            var toEmployee = New(vacation.UserId, vacation, "تم رفض طلب إجازتك",
                $"رُفض طلب إجازتك في مرحلة الاعتماد النهائي.{reasonSuffix}", NotificationType.VacationRejectedByBranchManager);

            var toFirstApprover = FirstApprover(vacation).Select(id => New(id, vacation, "رفض نهائي لإجازة",
                $"رُفضت نهائياً إجازة {employeeFullName} التي سبق ووافقت عليها.{reasonSuffix}",
                NotificationType.VacationRejectedByBranchManager));

            await notificationService.AddRangeAsync(toFirstApprover.Append(toEmployee));
        }

        public static async Task NotifyCancelledAsync(
            IUserPermissionService permissions,
            INotificationService notificationService,
            Vacation vacation,
            string employeeFullName,
            bool hadReachedBranchManager)
        {
            // قبل الموافقة الأولى: أصحاب الموافقة الأولى. بعدها: من وافق + أصحاب الاعتماد النهائي.
            var recipients = hadReachedBranchManager
                ? FirstApprover(vacation)
                    .Concat(await ApproversAsync(permissions, AppPermissions.ApproveVacationFinal, vacation, vacation.UserId))
                : await ApproversAsync(permissions, AppPermissions.ApproveVacationFirst, vacation, vacation.UserId);

            await notificationService.AddRangeAsync(recipients.Distinct().Select(id => New(id, vacation,
                "تم إلغاء طلب إجازة", $"ألغى {employeeFullName} طلب إجازته بنفسه", NotificationType.VacationCancelled)));
        }

        // ==================== دوال مساعدة ====================

        // أصحاب الصلاحية في فرع الإجازة، عدا صاحب الطلب
        private static async Task<List<int>> ApproversAsync(
            IUserPermissionService permissions, string permission, Vacation vacation, int excludeUserId) =>
            (await permissions.GetUsersWithPermissionAsync(permission, branchId: vacation.BranchId))
                .Where(u => u.Id != excludeUserId)
                .Select(u => u.Id)
                .ToList();

        private static IEnumerable<int> FirstApprover(Vacation vacation) =>
            vacation.FirstApprovedByUserId is int id && id != vacation.UserId ? [id] : [];

        private static Notification New(int userId, Vacation vacation, string title, string message, NotificationType type) => new()
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            RelatedEntityType = "Vacation",
            RelatedEntityId = vacation.Id,
            CreatedAt = DateTime.UtcNow
        };
    }
}
