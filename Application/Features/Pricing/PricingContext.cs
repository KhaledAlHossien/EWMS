using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using static Application.Features.Pricing.PricingSupport;

namespace Application.Features.Pricing
{
    /// <summary>
    /// مكتب التسعير
    /// تبعيات المعالجات المشتركة وما تشترك فيه من تحميل وحراسة (تركيب لا وراثة): كل حالة استخدام معالج مستقل يأخذ هذا السياق.
    /// </summary>
    public sealed class PricingContext(IPricingService pricing, IProjectService projects, IContractService contracts, IDiwanService diwan,
        IUserService users, IUserPermissionService permissions, INotificationService notifications)
    {
        public IPricingService Pricing { get; } = pricing;
        public IProjectService Projects { get; } = projects;
        public IContractService Contracts { get; } = contracts;
        public IDiwanService Diwan { get; } = diwan;
        public IUserService Users { get; } = users;
        public IUserPermissionService Permissions { get; } = permissions;
        public INotificationService Notifications { get; } = notifications;

        public Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(Users, Permissions);

        /// <summary>الطلب بكل ما تحته بعد التحقق من أنه ضمن حدّ المستخدم</summary>
        public async Task<PricingRequest> LoadAsync(Viewer viewer, int id) => Guard(viewer, await Pricing.GetByIdAsync(id));

        public async Task<(PricingRequest Request, PricingOffer Offer)> LoadOfferAsync(Viewer viewer, int offerId)
        {
            var request = Guard(viewer, await Pricing.GetByOfferAsync(offerId));
            return (request, request.Offers.First(o => o.Id == offerId));
        }

        public async Task<(PricingRequest Request, PricingOffer Offer, PricingOfferLine Line)> LoadLineAsync(Viewer viewer, int lineId)
        {
            var request = Guard(viewer, await Pricing.GetByLineAsync(lineId));
            var offer = request.Offers.First(o => o.Lines.Any(l => l.Id == lineId));
            return (request, offer, offer.Lines.First(l => l.Id == lineId));
        }

        private static PricingRequest Guard(Viewer viewer, PricingRequest? request)
        {
            if (request == null) throw new KeyNotFoundException("طلب التسعير غير موجود");
            PricingRules.EnsureCanView(viewer, request);
            return request;
        }

        public async Task<PricingAgreement> LoadAgreementAsync(Viewer viewer, int id)
        {
            var agreement = await Pricing.GetAgreementAsync(id) ?? throw new KeyNotFoundException("محضر الاتفاق غير موجود");
            PricingRules.EnsureCanViewAgreement(viewer, agreement);
            return agreement;
        }

        public async Task<PricingDetailDto> DetailAsync(Viewer viewer, int id)
        {
            var request = await Pricing.GetByIdAsync(id) ?? throw new KeyNotFoundException("طلب التسعير غير موجود");
            return ToDetail(request, viewer, DateTime.Today);
        }

        public async Task<PricingAgreementDetailDto> AgreementDetailAsync(Viewer viewer, int id)
        {
            var agreement = await Pricing.GetAgreementAsync(id) ?? throw new KeyNotFoundException("محضر الاتفاق غير موجود");
            return ToAgreementDetail(agreement, viewer);
        }

        /// <summary>فرع العملية: فرع المشروع إن رُبط، وإلا فرع المستخدم (ولنظرة المؤسسة أي فرع يُختار)</summary>
        public static int ResolveBranch(Viewer viewer, Project? project, int? requested)
        {
            if (project != null) return project.BranchId;
            var branch = viewer.IsOrganizationWide ? requested ?? viewer.User.BranchId : viewer.User.BranchId;
            return branch ?? throw new InvalidOperationException(viewer.IsOrganizationWide ? "اختر الفرع" : "حسابك غير مرتبط بفرع");
        }

        /// <summary>المعاملة الواردة المربوطة: من فرع الطلب ويراها المستخدم</summary>
        public async Task<Correspondence?> ResolveCorrespondenceAsync(Viewer viewer, int? id, int branchId)
        {
            if (id is not int correspondenceId) return null;
            var c = await Diwan.GetByIdAsync(correspondenceId) ?? throw new KeyNotFoundException("المعاملة غير موجودة");
            Application.Features.Diwan.DiwanRules.EnsureView(viewer, c);
            if (c.BranchId != branchId) throw new InvalidOperationException("المعاملة من فرع آخر");
            if (c.Direction != CorrespondenceDirection.Incoming) throw new InvalidOperationException("يُربط طلب التسعير بمعاملة واردة");
            return c;
        }

        /// <summary>إصدار وثيقة مثبَّت: من إضبارة المشروع المرتبط (ولا وثيقة لسجل بلا مشروع)</summary>
        public async Task<ProjectDocumentVersion?> ResolveVersionAsync(int? versionId, int? projectId)
        {
            if (versionId is not int id) return null;
            if (projectId is not int project) throw new InvalidOperationException("الوثائق تُرفع في إضبارة مشروع: اربط السجل بمشروع أولاً");
            var version = await Pricing.GetVersionAsync(id) ?? throw new KeyNotFoundException("إصدار الوثيقة غير موجود");
            if (version.Document.ProjectId != project) throw new InvalidOperationException("الوثيقة من مشروع آخر");
            return version;
        }

        /// <summary>المورد: من سجل مقاولي الفرع، نشط، ومن فرع السجل</summary>
        public async Task<Contractor> ResolveSupplierAsync(int supplierId)
        {
            return await Contracts.GetContractorAsync(supplierId) ?? throw new KeyNotFoundException("المورد غير موجود");
        }

        /// <summary>حاملو الصلاحية في الفرع</summary>
        public async Task<List<int>> HoldersAsync(int branchId, string permission) =>
            (await Permissions.GetUsersWithPermissionAsync(permission, branchId)).Select(u => u.Id).Distinct().ToList();

        /// <summary>إشعار لكل مستلم (بلا تكرار وبلا المنفِّذ) يفتح طلب التسعير من الواجهة</summary>
        public async Task NotifyAsync(IEnumerable<int> userIds, int exceptUserId, string entityType, int entityId, NotificationType type, string title, string message)
        {
            var now = DateTime.UtcNow;
            var rows = userIds.Distinct().Where(id => id != exceptUserId).Select(id => new Notification
            {
                UserId = id, Title = title, Message = message, Type = type, RelatedEntityType = entityType, RelatedEntityId = entityId, CreatedAt = now
            }).ToList();
            if (rows.Count > 0) await Notifications.AddRangeAsync(rows);
        }

        public Task NotifyRequestAsync(IEnumerable<int> userIds, int exceptUserId, PricingRequest request, NotificationType type, string title, string message) =>
            NotifyAsync(userIds, exceptUserId, AuditEntities.PricingRequest, request.Id, type, title, message);

        public static string Label(PricingRequest request) => $"{PricingRules.Number(request.Id, request.CreatedAt)} — {request.Title}";

        public static string Label(PricingAgreement agreement) =>
            $"{PricingRules.AgreementNumber(agreement.Id, agreement.CreatedAt)}{(string.IsNullOrWhiteSpace(agreement.Number) ? string.Empty : $" ({agreement.Number})")}";
    }
}
