using System.Linq.Expressions;
using Application.DTOs.Response;
using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.AssignedTasks
{
    /// <summary>
    /// قواعد لوحة المهام — Role-Permission فقط (قرار المستخدم 2026-10-03)، وحدّ كل صلاحية ثابت:
    /// - AssignTaskToDepartment: الإسناد لأقسام فرعي. AssignTaskToOffice: لمكاتب قسمي. AssignTaskToUser: لموظفي مكتبي.
    /// - HandleUnitTasks: أتولى المهام المسندة لوحدتي نفسها (قسمي إن كنت أتبع للقسم مباشرة، أو مكتبي) —
    ///   أغيّر حالتها وأفوّض أجزاء منها درجة واحدة نزولاً (مهمة قسم ← مكتب منه، مهمة مكتب ← موظف منه).
    /// - مهمة الموظف يتولاها الموظف نفسه بلا صلاحية.
    /// - المُسنِد يعدّل المهمة (ما لم تُنجز) ويحذفها (ما دامت لم تبدأ ولا مهام فرعية لها).
    /// - الاطلاع: SuperAdmin الكل، والمُسنِد والمنفِّذ، ومن يملك صلاحية إسناد/تولٍّ على مهام الوحدة التي تعمل عليها صلاحيته.
    /// </summary>
    public static class AssignedTaskRules
    {
        public const string RelatedEntityType = "AssignedTask";

        /// <summary>أنواع الجهات التي يستطيع المستخدم الإسناد إليها (قد تكون أكثر من نوع)</summary>
        public static List<AssignedTaskTargetType> TargetTypesFor(Viewer v)
        {
            var types = new List<AssignedTaskTargetType>();
            if (v.Has(AppPermissions.AssignTaskToDepartment) && v.User.BranchId != null) types.Add(AssignedTaskTargetType.Department);
            if (v.Has(AppPermissions.AssignTaskToOffice) && v.User.DepartmentId != null) types.Add(AssignedTaskTargetType.Office);
            if (v.Has(AppPermissions.AssignTaskToUser) && v.User.OfficeId != null) types.Add(AssignedTaskTargetType.User);
            return types;
        }

        /// <summary>الجهة التي تُفوَّض إليها أجزاء مهمة (درجة واحدة نزولاً)، أو null إن لم يكن لها تفويض</summary>
        public static AssignedTaskTargetType? DelegationTargetFor(AssignedTask t) => t.TargetType switch
        {
            AssignedTaskTargetType.Department => AssignedTaskTargetType.Office,
            AssignedTaskTargetType.Office => AssignedTaskTargetType.User,
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
        public static bool CanHandle(AssignedTask t, Viewer v) => t.TargetType switch
        {
            AssignedTaskTargetType.Department => v.Has(AppPermissions.HandleUnitTasks) && OrganizationRole.IsInDepartmentItself(v.User, t.DepartmentId),
            AssignedTaskTargetType.Office => v.Has(AppPermissions.HandleUnitTasks) && OrganizationRole.IsInOffice(v.User, t.OfficeId),
            AssignedTaskTargetType.User => t.AssigneeUserId == v.Id,
            _ => false
        };

        public static bool CanView(AssignedTask t, Viewer v) =>
            v.IsSuperAdmin || t.CreatedByUserId == v.Id || CanHandle(t, v) || Scope(v).Compile()(t);

        /// <summary>الواردة: المهام التي أنا منفِّذها</summary>
        public static Expression<Func<AssignedTask, bool>> Incoming(Viewer v)
        {
            var userId = v.Id;
            var handles = v.Has(AppPermissions.HandleUnitTasks);
            var ownDepartment = v.User.OfficeId == null ? v.User.DepartmentId : null; // أتبع للقسم مباشرة
            var ownOffice = v.User.OfficeId;
            return t => (t.TargetType == AssignedTaskTargetType.User && t.AssigneeUserId == userId)
                || (handles && ownDepartment != null && t.TargetType == AssignedTaskTargetType.Department && t.DepartmentId == ownDepartment)
                || (handles && ownOffice != null && t.TargetType == AssignedTaskTargetType.Office && t.OfficeId == ownOffice);
        }

        /// <summary>كل المهام التي أتابعها بحكم صلاحياتي (للتبويب "كل مهام نطاقي")</summary>
        public static Expression<Func<AssignedTask, bool>> Scope(Viewer v)
        {
            if (v.IsSuperAdmin) return t => true;

            var user = v.User;
            var handles = v.Has(AppPermissions.HandleUnitTasks);
            int? byBranch = v.Has(AppPermissions.AssignTaskToDepartment) ? user.BranchId : null;
            int? byDepartment = v.Has(AppPermissions.AssignTaskToOffice) || (handles && user.OfficeId == null) ? user.DepartmentId : null;
            int? byOffice = v.Has(AppPermissions.AssignTaskToUser) || handles ? user.OfficeId : null;
            var userId = user.Id;

            return t => t.CreatedByUserId == userId
                || (t.TargetType == AssignedTaskTargetType.User && t.AssigneeUserId == userId)
                || (byBranch != null && t.BranchId == byBranch)
                || (byDepartment != null && t.DepartmentId == byDepartment)
                || (byOffice != null && t.OfficeId == byOffice);
        }

        /// <summary>هل يظهر تبويب "كل مهام نطاقي"؟</summary>
        public static bool HasScope(Viewer v) =>
            v.IsSuperAdmin || TargetTypesFor(v).Count > 0 || v.Has(AppPermissions.HandleUnitTasks);

        /// <summary>المستخدمون الذين يتولون المهمة (لإرسال الإشعارات)</summary>
        public static async Task<List<int>> HandlerUserIdsAsync(IUserPermissionService permissions, AssignedTask t)
        {
            switch (t.TargetType)
            {
                case AssignedTaskTargetType.Department when t.DepartmentId is int d:
                    return (await permissions.GetUsersWithPermissionAsync(AppPermissions.HandleUnitTasks, departmentId: d, exactUnit: true))
                        .Select(u => u.Id).ToList();
                case AssignedTaskTargetType.Office when t.OfficeId is int o:
                    return (await permissions.GetUsersWithPermissionAsync(AppPermissions.HandleUnitTasks, officeId: o))
                        .Select(u => u.Id).ToList();
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

        public static AssignedTaskCardDto ToCard(AssignedTask t, Viewer viewer) => Fill(new AssignedTaskCardDto(), t, viewer);

        public static AssignedTaskDetailDto ToDetail(AssignedTask t, Viewer viewer)
        {
            var dto = Fill(new AssignedTaskDetailDto(), t, viewer);
            var isCreator = t.CreatedByUserId == viewer.Id;
            var handles = CanHandle(t, viewer);

            dto.Description = t.Description;
            dto.StartedAt = t.StartedAt;
            dto.CanEdit = isCreator && t.Status != AssignedTaskStatus.Done;
            dto.CanDelete = isCreator && t.Status == AssignedTaskStatus.Todo && t.SubTasks.Count == 0;
            dto.CanComment = CanView(t, viewer);
            // يفوّض من يتولى مهمة قسم (← مكاتبه) أو مهمة مكتب (← موظفيه)
            dto.CanDelegate = handles && t.Status != AssignedTaskStatus.Done && DelegationTargetFor(t) != null;
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

        private static T Fill<T>(T dto, AssignedTask t, Viewer viewer) where T : AssignedTaskCardDto
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
