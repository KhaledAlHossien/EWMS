using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.WorkTasks
{
    internal static class WorkTaskRules
    {
        /// <summary>
        /// كل الموظفين المسنَدة إليهم المهمة يجب أن يكونوا من فرع المهمة
        /// (المهام تختلف من فرع لآخر). SuperAdmin لا يتبع لفرع فلا يُسند له.
        /// </summary>
        public static async Task EnsureAssigneesInBranchAsync(
            IUserService userService, int branchId, IReadOnlyCollection<int> userIds)
        {
            if (userIds.Count == 0) return;

            var branchUserIds = (await userService.GetByBranchAsync(branchId))
                .Select(u => u.Id)
                .ToHashSet();

            if (userIds.Any(id => !branchUserIds.Contains(id)))
                throw new InvalidOperationException("لا يمكن إسناد المهمة إلا لموظفين من نفس الفرع");
        }

        /// <summary>
        /// من يرى بطاقة المهمة: SuperAdmin، أو الموظف المسنَدة إليه،
        /// أو رؤساء الفرع/القسم/المكتب في نفس الفرع (تظهر لهم في داشبوردات الفرع وتوزيع المهام).
        /// </summary>
        public static async Task EnsureCanViewAsync(
            IWorkTaskService workTaskService, User currentUser, WorkTask task)
        {
            var role = currentUser.Role?.Name ?? "";
            if (role == "SuperAdmin") return;

            if (await workTaskService.IsAssignedAsync(task.Id, currentUser.Id)) return;

            var isLeader = role is "BranchManager" or "Manager" or "OfficeManager";
            if (isLeader && currentUser.BranchId == task.BranchId) return;

            throw new UnauthorizedAccessException("لا تملك صلاحية عرض هذه المهمة");
        }

        /// <summary>إشعار الموظفين المسنَدين حديثاً فقط (لا نكرر الإشعار عند كل تعديل)</summary>
        public static async Task NotifyNewAssigneesAsync(
            INotificationService notificationService, WorkTask task, IEnumerable<int> newUserIds)
        {
            var notifications = newUserIds.Select(userId => new Notification
            {
                UserId = userId,
                Title = "مهمة عمل جديدة",
                Message = $"أُسندت إليك مهمة: {task.Name}",
                Type = NotificationType.WorkTaskAssigned,
                RelatedEntityType = "WorkTask",
                RelatedEntityId = task.Id,
                CreatedAt = DateTime.UtcNow
            });

            await notificationService.AddRangeAsync(notifications);
        }
    }
}
