using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.Vacations
{
    /// <summary>
    /// مسؤول عن إرسال إشعارات سير عمل الإجازة لكل الأطراف المعنية:
    /// الموظف صاحب الطلب، رئيس/رؤساء قسمه، رئيس/رؤساء فرعه.
    /// </summary>
    internal static class VacationNotifier
    {
        public static async Task NotifySubmittedAsync(
            IUserService userService,
            INotificationService notificationService,
            Vacation vacation,
            string employeeFullName)
        {
            var managers = await GetDepartmentManagersAsync(userService, vacation.DepartmentId);

            var notifications = managers.Select(m => new Notification
            {
                UserId = m.Id,
                Title = "طلب إجازة جديد",
                Message = $"قدّم {employeeFullName} طلب إجازة ({vacation.VacDayCount} يوم) بحاجة لموافقتك",
                Type = NotificationType.VacationSubmitted,
                RelatedEntityType = "Vacation",
                RelatedEntityId = vacation.Id,
                CreatedAt = DateTime.UtcNow
            });

            await notificationService.AddRangeAsync(notifications);
        }

        public static async Task NotifyApprovedByManagerAsync(
            IUserService userService,
            INotificationService notificationService,
            Vacation vacation,
            string employeeFullName)
        {
            var toEmployee = new Notification
            {
                UserId = vacation.UserId,
                Title = "تمت الموافقة على طلبك",
                Message = "وافق رئيس القسم على طلب إجازتك، وهو الآن بانتظار اعتماد رئيس الفرع",
                Type = NotificationType.VacationApprovedByManager,
                RelatedEntityType = "Vacation",
                RelatedEntityId = vacation.Id,
                CreatedAt = DateTime.UtcNow
            };

            var branchManagers = await GetBranchManagersAsync(userService, vacation.BranchId);

            var toBranchManagers = branchManagers.Select(bm => new Notification
            {
                UserId = bm.Id,
                Title = "طلب إجازة بانتظار اعتمادك",
                Message = $"طلب إجازة من {employeeFullName} وافق عليه رئيس القسم، وهو الآن بانتظار اعتمادك النهائي",
                Type = NotificationType.VacationForwardedToBranchManager,
                RelatedEntityType = "Vacation",
                RelatedEntityId = vacation.Id,
                CreatedAt = DateTime.UtcNow
            });

            await notificationService.AddRangeAsync(toBranchManagers.Append(toEmployee));
        }

        public static async Task NotifyRejectedByManagerAsync(
            INotificationService notificationService,
            Vacation vacation,
            string? reason)
        {
            var toEmployee = new Notification
            {
                UserId = vacation.UserId,
                Title = "تم رفض طلب إجازتك",
                Message = string.IsNullOrWhiteSpace(reason)
                    ? "رفض رئيس القسم طلب إجازتك"
                    : $"رفض رئيس القسم طلب إجازتك. السبب: {reason}",
                Type = NotificationType.VacationRejectedByManager,
                RelatedEntityType = "Vacation",
                RelatedEntityId = vacation.Id,
                CreatedAt = DateTime.UtcNow
            };

            await notificationService.AddAsync(toEmployee);
        }

        public static async Task NotifyApprovedFinalAsync(
            IUserService userService,
            INotificationService notificationService,
            Vacation vacation,
            string employeeFullName)
        {
            var toEmployee = new Notification
            {
                UserId = vacation.UserId,
                Title = "تم اعتماد إجازتك نهائياً",
                Message = "وافق رئيس الفرع على طلب إجازتك، وتم اعتمادها نهائياً",
                Type = NotificationType.VacationApprovedFinal,
                RelatedEntityType = "Vacation",
                RelatedEntityId = vacation.Id,
                CreatedAt = DateTime.UtcNow
            };

            var managers = await GetDepartmentManagersAsync(userService, vacation.DepartmentId);

            var toManagers = managers.Select(m => new Notification
            {
                UserId = m.Id,
                Title = "اعتماد نهائي لإجازة",
                Message = $"وافق رئيس الفرع نهائياً على إجازة {employeeFullName} التي سبق ووافقت عليها",
                Type = NotificationType.VacationApprovedFinal,
                RelatedEntityType = "Vacation",
                RelatedEntityId = vacation.Id,
                CreatedAt = DateTime.UtcNow
            });

            await notificationService.AddRangeAsync(toManagers.Append(toEmployee));
        }

        public static async Task NotifyRejectedByBranchManagerAsync(
            IUserService userService,
            INotificationService notificationService,
            Vacation vacation,
            string employeeFullName,
            string? reason)
        {
            var reasonSuffix = string.IsNullOrWhiteSpace(reason) ? "" : $" السبب: {reason}";

            var toEmployee = new Notification
            {
                UserId = vacation.UserId,
                Title = "تم رفض طلب إجازتك",
                Message = $"رفض رئيس الفرع طلب إجازتك بشكل نهائي.{reasonSuffix}",
                Type = NotificationType.VacationRejectedByBranchManager,
                RelatedEntityType = "Vacation",
                RelatedEntityId = vacation.Id,
                CreatedAt = DateTime.UtcNow
            };

            var managers = await GetDepartmentManagersAsync(userService, vacation.DepartmentId);

            var toManagers = managers.Select(m => new Notification
            {
                UserId = m.Id,
                Title = "رفض نهائي لإجازة",
                Message = $"رفض رئيس الفرع إجازة {employeeFullName} التي سبق ووافقت عليها.{reasonSuffix}",
                Type = NotificationType.VacationRejectedByBranchManager,
                RelatedEntityType = "Vacation",
                RelatedEntityId = vacation.Id,
                CreatedAt = DateTime.UtcNow
            });

            await notificationService.AddRangeAsync(toManagers.Append(toEmployee));
        }

        public static async Task NotifyCancelledAsync(
            IUserService userService,
            INotificationService notificationService,
            Vacation vacation,
            string employeeFullName,
            bool hadReachedBranchManager)
        {
            var recipients = await GetDepartmentManagersAsync(userService, vacation.DepartmentId);

            if (hadReachedBranchManager)
                recipients = recipients.Concat(await GetBranchManagersAsync(userService, vacation.BranchId)).ToList();

            var notifications = recipients.Select(r => new Notification
            {
                UserId = r.Id,
                Title = "تم إلغاء طلب إجازة",
                Message = $"ألغى {employeeFullName} طلب إجازته بنفسه",
                Type = NotificationType.VacationCancelled,
                RelatedEntityType = "Vacation",
                RelatedEntityId = vacation.Id,
                CreatedAt = DateTime.UtcNow
            });

            await notificationService.AddRangeAsync(notifications);
        }

        // ==================== دوال مساعدة ====================

        private static async Task<List<User>> GetDepartmentManagersAsync(IUserService userService, int departmentId)
        {
            var users = await userService.GetByDepartmentAsync(departmentId);
            return users.Where(u => u.Role?.Name == "Manager").ToList();
        }

        private static async Task<List<User>> GetBranchManagersAsync(IUserService userService, int branchId)
        {
            var users = await userService.GetByBranchAsync(branchId);
            return users.Where(u => u.Role?.Name == "BranchManager").ToList();
        }
    }
}
