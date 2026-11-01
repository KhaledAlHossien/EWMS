using Application.Common;
using Application.DTOs.Response;
using Application.Features.Pricing;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.Reconciliation
{
    /// <summary>
    /// مطابقة الفواتير
    /// تبعيات المعالجات المشتركة وما تشترك فيه من تحميل وحراسة (تركيب لا وراثة): كل حالة استخدام معالج مستقل يأخذ هذا السياق.
    /// </summary>
    public sealed class ReconciliationContext(IPriceReconciliationService reconciliations, IProjectService projects, IContractService contracts,
        IUserService users, IUserPermissionService permissions, INotificationService notifications)
    {
        public IPriceReconciliationService Reconciliations { get; } = reconciliations;
        public IProjectService Projects { get; } = projects;
        public IContractService Contracts { get; } = contracts;
        public IUserService Users { get; } = users;
        public IUserPermissionService Permissions { get; } = permissions;
        public INotificationService Notifications { get; } = notifications;

        public Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(Users, Permissions);

        public async Task<PriceReconciliation> LoadAsync(Viewer viewer, int id)
        {
            var reconciliation = await Reconciliations.GetByIdAsync(id) ?? throw new KeyNotFoundException("المطابقة غير موجودة");
            ReconciliationRules.EnsureCanView(viewer, reconciliation);
            return reconciliation;
        }

        public async Task<ReconciliationDetailDto> DetailAsync(Viewer viewer, int id)
        {
            var reconciliation = await Reconciliations.GetByIdAsync(id) ?? throw new KeyNotFoundException("المطابقة غير موجودة");
            return ReconciliationSupport.ToDetail(reconciliation, viewer);
        }

        public async Task<Contractor> ResolveSupplierAsync(int supplierId) =>
            await Contracts.GetContractorAsync(supplierId) ?? throw new KeyNotFoundException("المورد غير موجود");

        public async Task<Project?> ResolveProjectAsync(int? projectId) =>
            projectId is int id ? await Projects.GetByIdAsync(id) ?? throw new KeyNotFoundException("المشروع غير موجود") : null;

        /// <summary>العقد: من مشروع المطابقة نفسه</summary>
        public async Task<Contract?> ResolveContractAsync(int? contractId, Project? project)
        {
            if (contractId is not int id) return null;
            var contract = await Contracts.GetByIdAsync(id) ?? throw new KeyNotFoundException("العقد غير موجود");
            if (project == null || contract.ProjectId != project.Id) throw new InvalidOperationException("العقد من مشروع آخر: اختر مشروعه");
            return contract;
        }

        /// <summary>إصدار وثيقة مثبَّت: من إضبارة المشروع المرتبط (ولا وثيقة لسجل بلا مشروع)</summary>
        public async Task<ProjectDocumentVersion?> ResolveVersionAsync(int? versionId, int? projectId)
        {
            if (versionId is not int id) return null;
            if (projectId is not int project) throw new InvalidOperationException("الوثائق تُرفع في إضبارة مشروع: اربط المطابقة بمشروع أولاً");
            var version = await Reconciliations.GetVersionAsync(id) ?? throw new KeyNotFoundException("إصدار الوثيقة غير موجود");
            if (version.Document.ProjectId != project) throw new InvalidOperationException("الوثيقة من مشروع آخر");
            return version;
        }

        /// <summary>
        /// بنود المستند بمراجعها المجمَّدة: السطر المختار من طلب معتمد لهذا المورد في الفرع نفسه (سعره ووحدته وعملته وكمية بنده)،
        /// أو السعر المرجعي المعتمد للاستخدام الحالي لهذا المورد (سعره ووحدته وعملته)؛ والباقي بلا مرجع.
        /// </summary>
        public async Task<List<PriceReconciliationLine>> BuildLinesAsync(IReadOnlyList<DTOs.Request.ReconciliationLineRequestDto> input, int branchId, int supplierId)
        {
            var lines = new List<PriceReconciliationLine>();
            for (var i = 0; i < input.Count; i++)
            {
                var l = input[i];
                var line = new PriceReconciliationLine
                {
                    SortOrder = i + 1, Description = l.Description.Trim(), Unit = l.Unit.Trim(), InvoiceQuantity = l.Quantity, InvoiceUnitPrice = l.UnitPrice, Note = (l.Note ?? string.Empty).Trim()
                };
                switch ((ReconciliationReferenceKind)l.ReferenceKind)
                {
                    case ReconciliationReferenceKind.OfferLine:
                    {
                        var offerLine = await Reconciliations.GetOfferLineAsync(l.ReferenceId!.Value) ?? throw new KeyNotFoundException("سطر العرض المرجعي غير موجود");
                        var request = offerLine.PricingOffer.PricingRequest;
                        if (request.BranchId != branchId) throw new InvalidOperationException("سطر العرض من فرع آخر");
                        if (offerLine.PricingOffer.SupplierId != supplierId) throw new InvalidOperationException("سطر العرض لمورد آخر غير مورد المستند");
                        if (!offerLine.IsSelected || request.Status is not (PricingRequestStatus.Approved or PricingRequestStatus.ResultSent))
                            throw new InvalidOperationException("المرجع سطر عرض مختار من طلب معتمد فقط");
                        line.ReferenceKind = ReconciliationReferenceKind.OfferLine;
                        line.ReferenceOfferLineId = offerLine.Id;
                        line.ReferenceLabel = $"{PricingRules.Number(request.Id, request.CreatedAt)} — {offerLine.Item.Description}";
                        line.ReferenceQuantity = offerLine.Item.Quantity;
                        line.ReferenceUnitPrice = offerLine.UnitPrice;
                        line.ReferenceUnit = offerLine.Unit;
                        line.ReferenceCurrency = offerLine.PricingOffer.Currency;
                        break;
                    }
                    case ReconciliationReferenceKind.ReferencePrice:
                    {
                        var price = await Reconciliations.GetReferencePriceAsync(l.ReferenceId!.Value) ?? throw new KeyNotFoundException("السعر المرجعي غير موجود");
                        if (price.BranchId != branchId) throw new InvalidOperationException("السعر المرجعي من فرع آخر");
                        if (price.SupplierId != supplierId) throw new InvalidOperationException("السعر المرجعي لمورد آخر غير مورد المستند");
                        if (price.Status != ReferencePriceStatus.ApprovedForCurrentUse) throw new InvalidOperationException("المرجع سعر مرجعي معتمد للاستخدام الحالي فقط");
                        line.ReferenceKind = ReconciliationReferenceKind.ReferencePrice;
                        line.ReferencePriceId = price.Id;
                        line.ReferenceLabel = $"{ReferencePrices.ReferencePriceRules.Number(price.Id, price.CreatedAt)} — {price.ItemName}";
                        line.ReferenceUnitPrice = price.UnitPrice;
                        line.ReferenceUnit = price.Unit;
                        line.ReferenceCurrency = price.Currency;
                        break;
                    }
                    default:
                        line.ReferenceKind = ReconciliationReferenceKind.None;
                        break;
                }
                lines.Add(line);
            }
            return lines;
        }

        public static int ResolveBranch(Viewer viewer, int? requested) => PricingContext.ResolveBranch(viewer, null, requested);

        public static int ResolveBranch(Viewer viewer, Project? project, int? requested) => PricingContext.ResolveBranch(viewer, project, requested);

        public async Task<List<int>> HoldersAsync(int branchId, string permission) =>
            (await Permissions.GetUsersWithPermissionAsync(permission, branchId)).Select(u => u.Id).Distinct().ToList();

        public async Task NotifyAsync(IEnumerable<int> userIds, int exceptUserId, PriceReconciliation reconciliation, NotificationType type, string title, string message)
        {
            var now = DateTime.UtcNow;
            var rows = userIds.Distinct().Where(id => id != exceptUserId).Select(id => new Notification
            {
                UserId = id, Title = title, Message = message, Type = type, RelatedEntityType = AuditEntities.PriceReconciliation, RelatedEntityId = reconciliation.Id, CreatedAt = now
            }).ToList();
            if (rows.Count > 0) await Notifications.AddRangeAsync(rows);
        }

        public static string Label(PriceReconciliation r) =>
            $"{ReconciliationRules.Number(r.Id, r.CreatedAt)} ({ReconciliationRules.KindAr(r.DocumentKind)} {r.DocumentNumber})";
    }
}
