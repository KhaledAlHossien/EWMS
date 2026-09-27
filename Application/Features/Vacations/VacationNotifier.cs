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
            // إجازة رئيس القسم تبدأ مباشرة بانتظار رئيس الفرع
            var toBranch = vacation.Status == VacationStatus.PendingBranchManager;

            var recipients = toBranch
                ? await GetBranchManagersAsync(userService, vacation.BranchId, vacation.UserId)
                : await GetDepartmentManagersAsync(userService, vacation.DepartmentId, vacation.UserId);

            var notifications = recipients.Select(m => new Notification
            {
                UserId = m.Id,
                Title = toBranch ? "طلب إجازة بانتظار اعتمادك" : "طلب إجازة جديد",
                Message = toBranch
                    ? $"قدّم رئيس القسم {employeeFullName} طلب إجازة ({vacation.VacDayCount} يوم) بحاجة لاعتمادك"
                    : $"قدّم {employeeFullName} طلب إجازة ({vacation.VacDayCount} يوم) بحاجة لموافقتك",
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

            var branchManagers = await GetBranchManagersAsync(userService, vacation.BranchId, vacation.UserId);

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

            var managers = await GetParticipatingDepartmentManagersAsync(userService, vacation);

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

            var managers = await GetParticipatingDepartmentManagersAsync(userService, vacation);

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
            // إجازة رئيس القسم لم تمر على رؤساء القسم أصلاً → لا نبلغهم بإلغائها
            var skippedManagerStage = hadReachedBranchManager && !vacation.ManagerAccept;

            var recipients = skippedManagerStage
                ? new List<User>()
                : await GetDepartmentManagersAsync(userService, vacation.DepartmentId, vacation.UserId);

            if (hadReachedBranchManager)
                recipients = recipients.Concat(await GetBranchManagersAsync(userService, vacation.BranchId, vacation.UserId)).ToList();

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

        // excludeUserId: صاحب الطلب — رئيس القسم لا يُبلَّغ بإجازته الخاصة كأنه مراجِع لها
        private static async Task<List<User>> GetDepartmentManagersAsync(IUserService userService, int departmentId, int excludeUserId)
        {
            var users = await userService.GetByDepartmentAsync(departmentId);
            return users.Where(u => u.Role?.Name == "Manager" && u.Id != excludeUserId).ToList();
        }

        private static async Task<List<User>> GetBranchManagersAsync(IUserService userService, int branchId, int excludeUserId)
        {
            var users = await userService.GetByBranchAsync(branchId);
            return users.Where(u => u.Role?.Name == "BranchManager" && u.Id != excludeUserId).ToList();
        }

        // رؤساء القسم يُبلَّغون بالقرار النهائي فقط إن كانوا قد وافقوا على الطلب في المرحلة الأولى
        private static async Task<List<User>> GetParticipatingDepartmentManagersAsync(IUserService userService, Vacation vacation)
        {
            if (!vacation.ManagerAccept) return new List<User>();
            return await GetDepartmentManagersAsync(userService, vacation.DepartmentId, vacation.UserId);
        }
    }
}
