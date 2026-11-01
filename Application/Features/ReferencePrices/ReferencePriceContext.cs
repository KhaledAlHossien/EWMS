using Application.Common;
using Application.DTOs.Response;
using Application.Features.Pricing;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.ReferencePrices
{
    /// <summary>
    /// الأسعار المرجعية
    /// تبعيات المعالجات المشتركة وما تشترك فيه من تحميل وحراسة (تركيب لا وراثة): كل حالة استخدام معالج مستقل يأخذ هذا السياق.
    /// </summary>
    public sealed class ReferencePriceContext(IReferencePriceService prices, IProjectService projects, IContractService contracts,
        IUserService users, IUserPermissionService permissions, INotificationService notifications, IReferencePriceSpreadsheet spreadsheet)
    {
        public IReferencePriceService Prices { get; } = prices;
        public IProjectService Projects { get; } = projects;
        public IContractService Contracts { get; } = contracts;
        public IUserService Users { get; } = users;
        public IUserPermissionService Permissions { get; } = permissions;
        public INotificationService Notifications { get; } = notifications;
        public IReferencePriceSpreadsheet Spreadsheet { get; } = spreadsheet;

        public Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(Users, Permissions);

        /// <summary>السعر بعد التحقق من أنه ضمن حدّ المستخدم</summary>
        public async Task<ReferencePrice> LoadAsync(Viewer viewer, int id)
        {
            var price = await Prices.GetByIdAsync(id) ?? throw new KeyNotFoundException("السعر المرجعي غير موجود");
            ReferencePriceRules.EnsureCanView(viewer, price);
            return price;
        }

        public async Task<ReferencePriceDetailDto> DetailAsync(Viewer viewer, int id)
        {
            var price = await Prices.GetByIdAsync(id) ?? throw new KeyNotFoundException("السعر المرجعي غير موجود");
            var successor = await Prices.GetCorrectionOfAsync(id);
            var group = await Prices.GetGroupsAsync([price], includeSuperseded: true);
            return ReferencePriceSupport.ToDetail(price, viewer, successor, group);
        }

        /// <summary>المورد: من سجل مقاولي الفرع</summary>
        public async Task<Contractor> ResolveSupplierAsync(int supplierId) =>
            await Contracts.GetContractorAsync(supplierId) ?? throw new KeyNotFoundException("المورد غير موجود");

        public async Task<Project?> ResolveProjectAsync(int? projectId) =>
            projectId is int id ? await Projects.GetByIdAsync(id) ?? throw new KeyNotFoundException("المشروع غير موجود") : null;

        /// <summary>إصدار وثيقة مثبَّت: من إضبارة المشروع المرتبط (ولا وثيقة لسجل بلا مشروع)</summary>
        public async Task<ProjectDocumentVersion?> ResolveVersionAsync(int? versionId, int? projectId)
        {
            if (versionId is not int id) return null;
            if (projectId is not int project) throw new InvalidOperationException("الوثائق تُرفع في إضبارة مشروع: اربط السعر بمشروع أولاً");
            var version = await Prices.GetVersionAsync(id) ?? throw new KeyNotFoundException("إصدار الوثيقة غير موجود");
            if (version.Document.ProjectId != project) throw new InvalidOperationException("الوثيقة من مشروع آخر");
            return version;
        }

        /// <summary>فرع السجل: فرع المشروع إن رُبط، وإلا فرع المستخدم (ولنظرة المؤسسة أي فرع يُختار)</summary>
        public static int ResolveBranch(Viewer viewer, Project? project, int? requested) => PricingContext.ResolveBranch(viewer, project, requested);

        /// <summary>حاملو الصلاحية في الفرع</summary>
        public async Task<List<int>> HoldersAsync(int branchId, string permission) =>
            (await Permissions.GetUsersWithPermissionAsync(permission, branchId)).Select(u => u.Id).Distinct().ToList();

        /// <summary>إشعار لكل مستلم (بلا تكرار وبلا المنفِّذ) يفتح السعر المرجعي من الواجهة</summary>
        public async Task NotifyAsync(IEnumerable<int> userIds, int exceptUserId, ReferencePrice price, NotificationType type, string title, string message)
        {
            var now = DateTime.UtcNow;
            var rows = userIds.Distinct().Where(id => id != exceptUserId).Select(id => new Notification
            {
                UserId = id, Title = title, Message = message, Type = type, RelatedEntityType = AuditEntities.ReferencePrice, RelatedEntityId = price.Id, CreatedAt = now
            }).ToList();
            if (rows.Count > 0) await Notifications.AddRangeAsync(rows);
        }

        public static string Label(ReferencePrice price) => $"{ReferencePriceRules.Number(price.Id, price.CreatedAt)} — {price.ItemName}";
    }
}
