using System.Linq.Expressions;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.AssignedTasks
{
    /// <summary>
    /// قواعد لوحة المهام (قرار المستخدم 2026-09-28):
    /// - الإسناد نزولاً فقط: رئيس الفرع ← قسم من فرعه، رئيس القسم ← مكتب من قسمه، رئيس المكتب ← موظف من مكتبه.
    /// - مهمة القسم يتولاها رئيس القسم، ومهمة المكتب رئيس المكتب، ومهمة الموظف الموظف نفسه ("الجهة المنفِّذة").
    /// - المنفِّذ فقط يغيّر الحالة (السحب بين الأعمدة) ويفوّض أجزاء منها لجهة أدنى.
    /// - المُسنِد يعدّل المهمة (ما لم تُنجز) ويحذفها (ما دامت لم تبدأ ولا مهام فرعية لها).
    /// - الاطلاع: SuperAdmin الكل، والرؤساء على مهام نطاقهم، والمُسنِد والمنفِّذ.
    /// </summary>
    public static class AssignedTaskRules
    {
        public const string RelatedEntityType = "AssignedTask";

        /// <summary>نوع الجهة التي يستطيع الدور الإسناد إليها (null = لا يُسند)</summary>
        public static AssignedTaskTargetType? TargetTypeFor(string roleName) => roleName switch
        {
            "BranchManager" => AssignedTaskTargetType.Department,
            "Manager" => AssignedTaskTargetType.Office,
            "OfficeManager" => AssignedTaskTargetType.User,
            _ => null
        };

        public static string TargetTypeLabel(AssignedTaskTargetType? type) => type switch
        {
            AssignedTaskTargetType.Department => "قسم",
            AssignedTaskTargetType.Office => "مكتب",
            AssignedTaskTargetType.User => "موظف",
            _ => ""
        };

        /// <summary>هل المستخدم هو الجهة المنفِّذة للمهمة؟</summary>
        public static bool CanHandle(AssignedTask t, User user)
        {
            var role = user.Role?.Name ?? "";
            return t.TargetType switch
            {
                AssignedTaskTargetType.Department => role == "Manager" && user.DepartmentId == t.DepartmentId,
                AssignedTaskTargetType.Office => role == "OfficeManager" && user.OfficeId == t.OfficeId,
                AssignedTaskTargetType.User => t.AssigneeUserId == user.Id,
                _ => false
            };
        }

        public static bool CanView(AssignedTask t, User user)
        {
            var role = user.Role?.Name ?? "";
            return role == "SuperAdmin"
                || t.CreatedByUserId == user.Id
                || CanHandle(t, user)
                || (role == "BranchManager" && user.BranchId == t.BranchId)
                || (role == "Manager" && user.DepartmentId != null && user.DepartmentId == t.DepartmentId)
                || (role == "OfficeManager" && user.OfficeId != null && user.OfficeId == t.OfficeId);
        }

        /// <summary>الواردة: المهام التي أنا منفِّذها</summary>
        public static Expression<Func<AssignedTask, bool>> Incoming(User user)
        {
            var role = user.Role?.Name ?? "";
            var userId = user.Id;
            var departmentId = user.DepartmentId;
            var officeId = user.OfficeId;
            return role switch
            {
                "Manager" => t => (t.TargetType == AssignedTaskTargetType.Department && t.DepartmentId == departmentId)
                                  || (t.TargetType == AssignedTaskTargetType.User && t.AssigneeUserId == userId),
                "OfficeManager" => t => (t.TargetType == AssignedTaskTargetType.Office && t.OfficeId == officeId)
                                        || (t.TargetType == AssignedTaskTargetType.User && t.AssigneeUserId == userId),
                _ => t => t.TargetType == AssignedTaskTargetType.User && t.AssigneeUserId == userId
            };
        }

        /// <summary>كل المهام ضمن نطاق المستخدم (للاطلاع والمتابعة)</summary>
        public static Expression<Func<AssignedTask, bool>> Scope(User user)
        {
            var role = user.Role?.Name ?? "";
            var branchId = user.BranchId;
            var departmentId = user.DepartmentId;
            var officeId = user.OfficeId;
            return role switch
            {
                "SuperAdmin" => t => true,
                "BranchManager" => t => t.BranchId == branchId,
                "Manager" => t => t.DepartmentId == departmentId,
                "OfficeManager" => t => t.OfficeId == officeId,
                _ => Incoming(user)
            };
        }

        /// <summary>المستخدمون الذين يتولون المهمة (لإرسال الإشعارات)</summary>
        public static async Task<List<int>> HandlerUserIdsAsync(IUserService userService, AssignedTask t)
        {
            switch (t.TargetType)
            {
                case AssignedTaskTargetType.Department when t.DepartmentId is int d:
                    return (await userService.GetByDepartmentAsync(d))
                        .Where(u => u.IsActive && u.Role?.Name == "Manager").Select(u => u.Id).ToList();
                case AssignedTaskTargetType.Office when t.OfficeId is int o:
                    return (await userService.GetByOfficeAsync(o))
                        .Where(u => u.IsActive && u.Role?.Name == "OfficeManager").Select(u => u.Id).ToList();
                case AssignedTaskTargetType.User when t.AssigneeUserId is int u:
                    return [u];
                default:
                    return [];
            }
        }

        public static async Task NotifyAsync(
            INotificationService notificationService, IEnumerable<int> userIds, int excludeUserId,
            AssignedTask task, NotificationType type, string title, string message)
        {
            var notifications = userIds.Distinct().Where(id => id != excludeUserId).Select(id => new Notification
            {
                UserId = id,
                Title = title,
                Message = message,
                Type = type,
                RelatedEntityType = RelatedEntityType,
                RelatedEntityId = task.Id,
                CreatedAt = DateTime.UtcNow
            });
            await notificationService.AddRangeAsync(notifications);
        }

        public static AssignedTaskActivity Activity(AssignedTask task, User by, AssignedTaskActivityType type, string text,
            AssignedTaskStatus? from = null, AssignedTaskStatus? to = null) => new()
        {
            AssignedTaskId = task.Id,
            UserId = by.Id,
            Type = type,
            Text = text,
            FromStatus = from,
            ToStatus = to,
            CreatedAt = DateTime.UtcNow
        };

        // ════════════════════ العرض ════════════════════

        public static string StatusAr(AssignedTaskStatus s) => s switch
        {
            AssignedTaskStatus.Todo => "لم تُنفَّذ",
            AssignedTaskStatus.InProgress => "قيد التنفيذ",
            AssignedTaskStatus.Done => "تم التنفيذ",
            _ => ""
        };

        public static string PriorityAr(AssignedTaskPriority p) => p switch
        {
            AssignedTaskPriority.Low => "منخفضة",
            AssignedTaskPriority.Normal => "عادية",
            AssignedTaskPriority.High => "مرتفعة",
            AssignedTaskPriority.Urgent => "عاجلة",
            _ => ""
        };

        public static string TargetName(AssignedTask t) => t.TargetType switch
        {
            AssignedTaskTargetType.Department => t.Department?.Name ?? "",
            AssignedTaskTargetType.Office => t.Office?.Name ?? "",
            AssignedTaskTargetType.User => t.AssigneeUser?.FullName ?? "",
            _ => ""
        };

        private static string TargetPath(AssignedTask t) => string.Join(" / ", new[]
        {
            t.Branch?.Name, t.Department?.Name, t.TargetType == AssignedTaskTargetType.User ? t.Office?.Name : null
        }.Where(s => !string.IsNullOrEmpty(s)));

        public static bool IsOverdue(AssignedTask t) =>
            t.Status != AssignedTaskStatus.Done && t.DueDate != null && t.DueDate.Value.Date < DateTime.Today;

        public static AssignedTaskCardDto ToCard(AssignedTask t, User viewer) => Fill(new AssignedTaskCardDto(), t, viewer);

        public static AssignedTaskDetailDto ToDetail(AssignedTask t, User viewer)
        {
            var dto = Fill(new AssignedTaskDetailDto(), t, viewer);
            var isCreator = t.CreatedByUserId == viewer.Id;
            var handles = CanHandle(t, viewer);
            var viewerTarget = TargetTypeFor(viewer.Role?.Name ?? "");

            dto.Description = t.Description;
            dto.StartedAt = t.StartedAt;
            dto.CanEdit = isCreator && t.Status != AssignedTaskStatus.Done;
            dto.CanDelete = isCreator && t.Status == AssignedTaskStatus.Todo && t.SubTasks.Count == 0;
            dto.CanComment = CanView(t, viewer);
            // يفوّض من يتولى مهمة قسم (رئيس القسم ← مكاتبه) أو مهمة مكتب (رئيس المكتب ← موظفيه)
            dto.CanDelegate = handles && t.Status != AssignedTaskStatus.Done
                && ((t.TargetType == AssignedTaskTargetType.Department && viewerTarget == AssignedTaskTargetType.Office)
                    || (t.TargetType == AssignedTaskTargetType.Office && viewerTarget == AssignedTaskTargetType.User));
            dto.SubTasks = t.SubTasks.OrderBy(s => s.CreatedAt).Select(s => ToCard(s, viewer)).ToList();
            dto.Activities = t.Activities.OrderBy(a => a.CreatedAt).Select(a => new AssignedTaskActivityDto
            {
                Id = a.Id,
                Type = a.Type.ToString(),
                Text = a.Text,
                UserName = a.User?.FullName ?? "",
                CreatedAt = a.CreatedAt
            }).ToList();
            return dto;
        }

        private static T Fill<T>(T dto, AssignedTask t, User viewer) where T : AssignedTaskCardDto
        {
            dto.Id = t.Id;
            dto.Title = t.Title;
            dto.Priority = t.Priority.ToString();
            dto.PriorityAr = PriorityAr(t.Priority);
            dto.Status = t.Status.ToString();
            dto.StatusAr = StatusAr(t.Status);
            dto.DueDate = t.DueDate;
            dto.IsOverdue = IsOverdue(t);
            dto.TargetType = t.TargetType.ToString();
            dto.TargetName = TargetName(t);
            dto.TargetPath = TargetPath(t);
            dto.CreatedByUserId = t.CreatedByUserId;
            dto.CreatedByName = t.CreatedByUser?.FullName ?? "";
            dto.ParentTaskId = t.ParentTaskId;
            dto.ParentTitle = t.ParentTask?.Title;
            dto.SubTasksTotal = t.SubTasks.Count;
            dto.SubTasksDone = t.SubTasks.Count(s => s.Status == AssignedTaskStatus.Done);
            dto.CommentsCount = t.Activities.Count(a => a.Type == AssignedTaskActivityType.Comment);
            dto.CreatedAt = t.CreatedAt;
            dto.UpdatedAt = t.UpdatedAt;
            dto.CompletedAt = t.CompletedAt;
            dto.CanChangeStatus = CanHandle(t, viewer);
            return dto;
        }
    }
}
