using Application.Common;
using Application.DTOs.Response;
using Application.Features.Projects;
using Application.Features.Schedules;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using static Application.Features.Studies.StudySupport;

namespace Application.Features.Studies
{
    /// <summary>
    /// الدراسات والمكاتب الفنية
    /// تبعيات المعالجات المشتركة وما تشترك فيه من تحميل وحراسة (تركيب لا وراثة): كل حالة استخدام معالج مستقل يأخذ هذا السياق.
    /// </summary>
    public sealed class StudyContext(IStudyService studies, IProjectService projects, IDiwanService diwan, IOfficeService offices,
        IUserService users, IUserPermissionService permissions, INotificationService notifications)
    {
        public IStudyService Studies { get; } = studies;
        public IProjectService Projects { get; } = projects;
        public IDiwanService Diwan { get; } = diwan;
        public IOfficeService Offices { get; } = offices;
        public IUserService Users { get; } = users;
        public IUserPermissionService Permissions { get; } = permissions;
        public INotificationService Notifications { get; } = notifications;

        public Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(Users, Permissions);

        /// <summary>الأمر بمخرجاته بعد التحقق من أن مشروعه ضمن حدّ المستخدم وأنه يملك صلاحية دراسات</summary>
        public async Task<StudyOrder> LoadAsync(Viewer viewer, int id)
        {
            var order = await Studies.GetByIdAsync(id) ?? throw new KeyNotFoundException("أمر الدراسة غير موجود");
            StudyRules.EnsureCanView(viewer, order.Project);
            return order;
        }

        /// <summary>المخرج مع أمره (الحدّ نفسه)</summary>
        public async Task<(StudyOrder Order, StudyDeliverable Deliverable)> LoadDeliverableAsync(Viewer viewer, int deliverableId)
        {
            var order = await Studies.GetByDeliverableAsync(deliverableId) ?? throw new KeyNotFoundException("المخرج غير موجود");
            StudyRules.EnsureCanView(viewer, order.Project);
            return (order, order.Deliverables.First(d => d.Id == deliverableId));
        }

        public async Task<StudyDetailDto> DetailAsync(Viewer viewer, int id)
        {
            var order = await Studies.GetByIdAsync(id) ?? throw new KeyNotFoundException("أمر الدراسة غير موجود");
            return ToDetail(order, viewer, DateTime.Today);
        }

        /// <summary>
        /// فرع العملية على أنواع المخرجات: فرع المستخدم، ولنظرة المؤسسة أي فرع. من لا فرع له ولا نظرة مؤسسة لا يعمل فيها.
        /// </summary>
        public static int ResolveBranch(Viewer viewer, int? requested)
        {
            var branch = viewer.IsOrganizationWide ? requested ?? viewer.User.BranchId : viewer.User.BranchId;
            return branch ?? throw new InvalidOperationException(viewer.IsOrganizationWide ? "اختر الفرع" : "حسابك غير مرتبط بفرع");
        }

        public static void EnsureBranchAccess(Viewer viewer, int branchId)
        {
            if (!viewer.IsOrganizationWide && !OrganizationRole.InBranch(viewer.User, branchId))
                throw new UnauthorizedAccessException("لا تملك صلاحية على إعدادات فرع آخر");
        }

        /// <summary>يملك أحد صلاحيات إدارة الأنواع (فيرى المعطّلة منها)</summary>
        public static bool CanManageTypes(Viewer viewer) =>
            viewer.IsSuperAdmin || viewer.Has(AppPermissions.CreateDeliverableType)
            || viewer.Has(AppPermissions.EditDeliverableType) || viewer.Has(AppPermissions.DeleteDeliverableType);

        /// <summary>المعاملة الواردة المربوطة: من فرع المشروع ويراها المستخدم</summary>
        public async Task<Correspondence?> ResolveCorrespondenceAsync(Viewer viewer, int? id, int branchId)
        {
            if (id is not int correspondenceId) return null;
            var c = await Diwan.GetByIdAsync(correspondenceId) ?? throw new KeyNotFoundException("المعاملة غير موجودة");
            Application.Features.Diwan.DiwanRules.EnsureView(viewer, c);
            if (c.BranchId != branchId) throw new InvalidOperationException("المعاملة من فرع آخر غير فرع المشروع");
            return c;
        }

        /// <summary>
        /// المسند إليه والمكتب بعد التحقق: مهندس من مشاركي المشروع (والمكتب من فرعه) — القواعد في <see cref="StudyRules.ValidateAssignment"/>.
        /// </summary>
        public async Task<(User? Assignee, Office? Office)> ResolveAssignmentAsync(Project project, int? assigneeId, int? officeId)
        {
            var assignee = assigneeId is int a ? await Users.GetByIdAsync(a) ?? throw new KeyNotFoundException("المهندس غير موجود") : null;
            var office = officeId is int o ? await Offices.GetByIdAsync(o) ?? throw new KeyNotFoundException("المكتب غير موجود") : null;
            if (StudyRules.ValidateAssignment(project, assignee, office) is string problem) throw new InvalidOperationException(problem);
            return (assignee, office);
        }

        /// <summary>حاملو الصلاحية في فرع المشروع ممن يرونه بحدّه</summary>
        public Task<List<int>> HoldersAsync(Project project, string permission) =>
            ScheduleNotifier.HoldersAsync(Permissions, project, permission);

        /// <summary>إشعار لكل مستلم (بلا تكرار وبلا المنفِّذ) يفتح أمر الدراسة من الواجهة</summary>
        public async Task NotifyAsync(IEnumerable<int> userIds, int exceptUserId, StudyOrder order, NotificationType type, string title, string message)
        {
            var now = DateTime.UtcNow;
            var rows = userIds.Distinct().Where(id => id != exceptUserId).Select(id => new Notification
            {
                UserId = id, Title = title, Message = message, Type = type,
                RelatedEntityType = AuditEntities.Study, RelatedEntityId = order.Id, CreatedAt = now
            }).ToList();
            if (rows.Count > 0) await Notifications.AddRangeAsync(rows);
        }

        public static string Label(StudyOrder order) => $"{StudyRules.Number(order.Id, order.CreatedAt)} — {order.Title}";
    }
}
