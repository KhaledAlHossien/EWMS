using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using static Application.Features.Diwan.DiwanSupport;

namespace Application.Features.Diwan
{
    /// <summary>
    /// الديوان
    /// تبعيات المعالجات المشتركة وما تشترك فيه من تحميل وحراسة (تركيب لا وراثة): كل حالة استخدام معالج مستقل يأخذ هذا السياق.
    /// </summary>
    public sealed class DiwanContext(IDiwanService diwan, IProjectService projects, IDepartmentService departments, IOfficeService offices,
        IUserService users, IUserPermissionService permissions, INotificationService notifications, IFileStore files, IUserSignatureService signatures)
    {
        public IDiwanService Diwan { get; } = diwan;
        public IProjectService Projects { get; } = projects;
        public IDepartmentService Departments { get; } = departments;
        public IOfficeService Offices { get; } = offices;
        public IUserService Users { get; } = users;
        public IUserPermissionService Permissions { get; } = permissions;
        public INotificationService Notifications { get; } = notifications;
        public IFileStore Files { get; } = files;
        public IUserSignatureService Signatures { get; } = signatures;

        public Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(Users, Permissions);

        /// <summary>المعاملة المحمَّلة بتفاصيلها بعد التحقق من أنها ضمن حدّ المستخدم</summary>
        public async Task<Correspondence> LoadAsync(Viewer viewer, int id)
        {
            var c = await Diwan.GetByIdAsync(id) ?? throw new KeyNotFoundException("المعاملة غير موجودة");
            DiwanRules.EnsureView(viewer, c);
            return c;
        }

        /// <summary>
        /// فرع العملية: فرع المستخدم، ولنظرة المؤسسة أي فرع يختاره. من لا فرع له ولا نظرة مؤسسة لا يعمل في الديوان.
        /// </summary>
        public static int ResolveBranch(Viewer viewer, int? requested)
        {
            var branch = viewer.IsOrganizationWide ? requested ?? viewer.User.BranchId : viewer.User.BranchId;
            return branch ?? throw new InvalidOperationException(viewer.IsOrganizationWide ? "اختر الفرع" : "حسابك غير مرتبط بفرع");
        }

        /// <summary>إعدادات الفرع (أنواع وحالات): لمستخدمي الفرع نفسه، ولنظرة المؤسسة أي فرع</summary>
        public static void EnsureBranchAccess(Viewer viewer, int branchId)
        {
            if (!viewer.IsOrganizationWide && !OrganizationRole.InBranch(viewer.User, branchId))
                throw new UnauthorizedAccessException("لا تملك صلاحية على إعدادات فرع آخر");
        }

        /// <summary>يملك أحد صلاحيات إدارة الأنواع والحالات (فيرى المعطّلة منها)</summary>
        public static bool CanManageLookups(Viewer viewer) =>
            viewer.IsSuperAdmin || viewer.Has(AppPermissions.CreateCorrespondenceLookup)
            || viewer.Has(AppPermissions.EditCorrespondenceLookup) || viewer.Has(AppPermissions.DeleteCorrespondenceLookup);

        /// <summary>نوع فعّال من فرع المعاملة</summary>
        public async Task<CorrespondenceType> ResolveTypeAsync(int branchId, int typeId)
        {
            var type = await Diwan.GetTypeAsync(typeId) ?? throw new KeyNotFoundException("نوع المعاملة غير موجود");
            if (type.BranchId != branchId) throw new InvalidOperationException("النوع المختار يتبع فرعاً آخر");
            if (!type.IsActive) throw new InvalidOperationException("هذا النوع معطّل");
            return type;
        }

        /// <summary>مشروع اختياري: يراه المستخدم ويتبع فرع المعاملة (null إن لم يُختر)</summary>
        public async Task<Project?> ResolveProjectAsync(Viewer viewer, int? projectId, int branchId)
        {
            if (projectId is not int id) return null;
            var project = await Projects.GetByIdAsync(id) ?? throw new KeyNotFoundException("المشروع غير موجود");
            Features.Projects.ProjectRules.EnsureView(viewer, project);
            if (project.BranchId != branchId) throw new InvalidOperationException("المشروع يتبع فرعاً آخر غير فرع المعاملة");
            return project;
        }

        /// <summary>الوارد الذي يجيب عنه الصادر: وارد من فرع الصادر يراه المستخدم</summary>
        public async Task<Correspondence?> ResolveReplyToAsync(Viewer viewer, int? replyToId, int branchId)
        {
            if (replyToId is not int id) return null;
            var original = await Diwan.GetByIdAsync(id) ?? throw new KeyNotFoundException("المعاملة المردود عليها غير موجودة");
            DiwanRules.EnsureView(viewer, original);
            if (original.Direction != CorrespondenceDirection.Incoming || original.BranchId != branchId)
                throw new InvalidOperationException("يُردّ على وارد من الفرع نفسه");
            return original;
        }

        /// <summary>التفصيل بعد إعادة التحميل (يعكس ما حُفظ)</summary>
        public async Task<CorrespondenceResponseDto> DetailAsync(Viewer viewer, int id)
        {
            var c = await Diwan.GetByIdAsync(id) ?? throw new KeyNotFoundException("المعاملة غير موجودة");
            return ToDetail(c, viewer, DateTime.Today);
        }

        /// <summary>إشعار لكل مستلم (بلا تكرار وبلا المنفِّذ) يفتح المعاملة من الواجهة</summary>
        public async Task NotifyAsync(IEnumerable<int> userIds, int exceptUserId, Correspondence c, NotificationType type, string title, string message)
        {
            var now = DateTime.UtcNow;
            var rows = userIds.Distinct().Where(id => id != exceptUserId).Select(id => new Notification
            {
                UserId = id,
                Title = title,
                Message = message,
                Type = type,
                RelatedEntityType = "Correspondence",
                RelatedEntityId = c.Id,
                CreatedAt = now
            }).ToList();
            if (rows.Count > 0) await Notifications.AddRangeAsync(rows);
        }

        /// <summary>أرقام من يعالج وحدة معيّنة (قسم بذاته أو مكتب) ومن سُمّي بالاسم</summary>
        public async Task<List<int>> HandlerIdsAsync(int departmentId, int? officeId, int? userId)
        {
            var holders = officeId is int office
                ? await Permissions.GetUsersWithPermissionAsync(AppPermissions.HandleCorrespondence, officeId: office)
                : await Permissions.GetUsersWithPermissionAsync(AppPermissions.HandleCorrespondence, departmentId: departmentId, exactUnit: true);
            var ids = holders.Select(u => u.Id).ToList();
            if (userId is int named) ids.Add(named);
            return ids;
        }
    }
}
