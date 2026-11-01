using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using static Application.Features.Pricing.PricingSupport;

namespace Application.Features.ReferencePrices
{
    // الأسعار المرجعية: بحث بحدّ الفرع، تفصيل بتاريخ المادة، إضافة، تصحيح بسجل بديل، واعتماد «للاستخدام الحالي» لغير من أضاف. لا تعديل ولا حذف.

    public record SearchReferencePricesQuery(int? BranchId, string? Search, int? SupplierId, int? ProjectId, int? Status, bool IncludeSuperseded, DateTime? From, DateTime? To, int Page, int PageSize)
        : IRequest<PagedResultDto<ReferencePriceListItemDto>>;
    public record GetReferencePriceQuery(int Id) : IRequest<ReferencePriceDetailDto>;
    public record CreateReferencePriceCommand(ReferencePriceRequestDto Dto) : IRequest<ReferencePriceDetailDto>;
    public record CorrectReferencePriceCommand(int Id, CorrectReferencePriceRequestDto Dto) : IRequest<ReferencePriceDetailDto>;
    public record ApproveReferencePriceCommand(int Id, PricingStepRequestDto Dto) : IRequest<ReferencePriceDetailDto>;

    internal static class ReferencePriceInput
    {
        public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        public static string? Validate(string? code, string? name, string? specs, string? unit, decimal price, string? currency, DateTime date, int source, string? sourceDocument, string? notes) =>
            ReferencePriceRules.ValidateInput(code, name, specs, unit, price, currency, date, source, sourceDocument, notes, DateTime.Today);
    }

    public class SearchReferencePricesHandler(ReferencePriceContext ctx) : IRequestHandler<SearchReferencePricesQuery, PagedResultDto<ReferencePriceListItemDto>>
    {
        public async Task<PagedResultDto<ReferencePriceListItemDto>> Handle(SearchReferencePricesQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            ReferencePriceRules.EnsureCanViewAny(viewer);
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);

            var branch = viewer.IsOrganizationWide ? request.BranchId : viewer.User.BranchId;
            if (!viewer.IsOrganizationWide && branch == null) return new PagedResultDto<ReferencePriceListItemDto> { Items = [], TotalCount = 0, Page = page, PageSize = pageSize };
            ReferencePriceStatus? status = request.Status is int s && Enum.IsDefined(typeof(ReferencePriceStatus), s) ? (ReferencePriceStatus)s : null;

            var (items, total) = await ctx.Prices.SearchAsync(new ReferencePriceFilter(branch, request.Search, request.SupplierId, request.ProjectId, status, request.IncludeSuperseded, request.From, request.To), page, pageSize);
            var group = await ctx.Prices.GetGroupsAsync(items, includeSuperseded: false);
            var (latest, latestForSupplier) = ReferencePriceRules.Latest(group);
            return new PagedResultDto<ReferencePriceListItemDto>
            {
                Items = items.Select(p => ReferencePriceSupport.ToListItem(p, latest, latestForSupplier)).ToList(), TotalCount = total, Page = page, PageSize = pageSize
            };
        }
    }

    public class GetReferencePriceHandler(ReferencePriceContext ctx) : IRequestHandler<GetReferencePriceQuery, ReferencePriceDetailDto>
    {
        public async Task<ReferencePriceDetailDto> Handle(GetReferencePriceQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            await ctx.LoadAsync(viewer, request.Id);
            return await ctx.DetailAsync(viewer, request.Id);
        }
    }

    public class CreateReferencePriceHandler(ReferencePriceContext ctx) : IRequestHandler<CreateReferencePriceCommand, ReferencePriceDetailDto>
    {
        public async Task<ReferencePriceDetailDto> Handle(CreateReferencePriceCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var supplier = await ctx.ResolveSupplierAsync(dto.SupplierId);
            var project = await ctx.ResolveProjectAsync(dto.ProjectId);
            var branch = ReferencePriceContext.ResolveBranch(viewer, project, dto.BranchId);
            ReferencePriceRules.EnsureCanAdd(viewer, branch, supplier, project);
            if (ReferencePriceInput.Validate(dto.ItemCode, dto.ItemName, dto.Specifications, dto.Unit, dto.UnitPrice, dto.Currency, dto.PriceDate, dto.Source, dto.SourceDocument, dto.Notes) is string problem)
                throw new InvalidOperationException(problem);
            var version = await ctx.ResolveVersionAsync(dto.DocumentVersionId, project?.Id);

            var created = await ctx.Prices.AddAsync(New(dto.ItemCode, dto.ItemName, dto.Specifications, dto.Unit, dto.SupplierId, dto.UnitPrice, dto.Currency, dto.PriceDate, project?.Id,
                dto.Source, dto.SourceDocument, version?.Id, dto.Notes, branch, viewer.Id),
                new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.ReferencePrice, null, $"إضافة سعر مرجعي «{dto.ItemName.Trim()}» عند «{supplier.Name}»",
                    Lines($"السعر: {dto.UnitPrice:0.####} {dto.Currency} / {dto.Unit.Trim()}", $"التاريخ: {dto.PriceDate:yyyy-MM-dd}", $"المصدر: {dto.SourceDocument.Trim()}")));

            await ctx.NotifyAsync(await ctx.HoldersAsync(branch, AppPermissions.ApproveReferencePrice), viewer.Id, created, NotificationType.ReferencePriceAwaiting,
                "سعر مرجعي بانتظار اعتمادك", $"أضاف {viewer.User.FullName} السعر {ReferencePriceContext.Label(created)}");
            return await ctx.DetailAsync(viewer, created.Id);
        }

        internal static ReferencePrice New(string? code, string name, string? specs, string unit, int supplierId, decimal price, string currency, DateTime date, int? projectId,
            int source, string sourceDocument, int? versionId, string? notes, int branchId, int userId) => new()
        {
            BranchId = branchId, ItemCode = ReferencePriceInput.Clean(code), ItemName = name.Trim(), Specifications = (specs ?? string.Empty).Trim(), Unit = unit.Trim(),
            SupplierId = supplierId, UnitPrice = price, Currency = currency, PriceDate = date.Date, ProjectId = projectId, Source = (ReferencePriceSource)source,
            SourceDocument = sourceDocument.Trim(), DocumentVersionId = versionId, Notes = (notes ?? string.Empty).Trim(), Status = ReferencePriceStatus.Historical, CreatedByUserId = userId
        };
    }

    /// <summary>
    /// التصحيح بسجل بديل: بيانات السجل الجديد كاملة وسبب التصحيح، والمُصحَّح يبقى في التاريخ (يصير «مُستبدَلاً» فوراً إن كان تاريخياً،
    /// وبعد اعتماد بديله إن كان معتمداً). البديل تاريخي بانتظار الاعتماد.
    /// </summary>
    public class CorrectReferencePriceHandler(ReferencePriceContext ctx) : IRequestHandler<CorrectReferencePriceCommand, ReferencePriceDetailDto>
    {
        public async Task<ReferencePriceDetailDto> Handle(CorrectReferencePriceCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var original = await ctx.LoadAsync(viewer, request.Id);
            ReferencePriceRules.EnsureCanCorrect(viewer, original, dto.Reason);
            if (await ctx.Prices.GetCorrectionOfAsync(original.Id) != null) throw new InvalidOperationException("لهذا السعر تصحيح قائم: صحّح السجل الأحدث");

            var supplier = dto.SupplierId == original.SupplierId ? original.Supplier : await ctx.ResolveSupplierAsync(dto.SupplierId);
            var project = dto.ProjectId == original.ProjectId ? original.Project : await ctx.ResolveProjectAsync(dto.ProjectId);
            ReferencePriceRules.EnsureCanAdd(viewer, original.BranchId, supplier, project);
            if (ReferencePriceInput.Validate(dto.ItemCode, dto.ItemName, dto.Specifications, dto.Unit, dto.UnitPrice, dto.Currency, dto.PriceDate, dto.Source, dto.SourceDocument, dto.Notes) is string problem)
                throw new InvalidOperationException(problem);
            var version = await ctx.ResolveVersionAsync(dto.DocumentVersionId, project?.Id);

            var replacement = CreateReferencePriceHandler.New(dto.ItemCode, dto.ItemName, dto.Specifications, dto.Unit, dto.SupplierId, dto.UnitPrice, dto.Currency, dto.PriceDate, project?.Id,
                dto.Source, dto.SourceDocument, version?.Id, dto.Notes, original.BranchId, viewer.Id);
            replacement.CorrectionReason = dto.Reason.Trim();

            var details = Lines($"يصحّح: {ReferencePriceRules.Number(original.Id, original.CreatedAt)}", $"السبب: {dto.Reason.Trim()}",
                Change("المادة", original.ItemName, dto.ItemName.Trim()), Change("المورد", original.Supplier.Name, supplier.Name),
                Change("السعر", $"{original.UnitPrice:0.####} {original.Currency} / {original.Unit}", $"{dto.UnitPrice:0.####} {dto.Currency} / {dto.Unit.Trim()}"),
                Change("التاريخ", original.PriceDate.ToString("yyyy-MM-dd"), dto.PriceDate.ToString("yyyy-MM-dd")), Change("المصدر", original.SourceDocument, dto.SourceDocument.Trim()));
            var created = await ctx.Prices.CorrectAsync(replacement, original, ReferencePriceRules.StatusAfterCorrection(original.Status), dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.ReferencePrice, null, $"تصحيح السعر المرجعي {ReferencePriceContext.Label(original)} بسجل بديل", details));

            await ctx.NotifyAsync(await ctx.HoldersAsync(original.BranchId, AppPermissions.ApproveReferencePrice), viewer.Id, created, NotificationType.ReferencePriceAwaiting,
                "سعر مرجعي بانتظار اعتمادك", $"صحّح {viewer.User.FullName} السعر {ReferencePriceContext.Label(original)} بسجل بديل");
            return await ctx.DetailAsync(viewer, created.Id);
        }
    }

    /// <summary>الاعتماد «للاستخدام الحالي»: لغير من أضاف السعر (حتى مدير النظام)، ويصير أسلافه المعتمدون «مُستبدَلين»</summary>
    public class ApproveReferencePriceHandler(ReferencePriceContext ctx) : IRequestHandler<ApproveReferencePriceCommand, ReferencePriceDetailDto>
    {
        public async Task<ReferencePriceDetailDto> Handle(ApproveReferencePriceCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var price = await ctx.LoadAsync(viewer, request.Id);
            ReferencePriceRules.EnsureCanApprove(viewer, price, request.Dto.Note);

            var ancestors = await ctx.Prices.GetApprovedAncestorsAsync(price);
            price.Status = ReferencePriceStatus.ApprovedForCurrentUse;
            price.ApprovedByUserId = viewer.Id;
            price.ApprovedAt = DateTime.UtcNow;
            price.ApprovalNote = (request.Dto.Note ?? string.Empty).Trim();
            await ctx.Prices.ApproveAsync(price, ancestors, request.Dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Approved, AuditEntities.ReferencePrice, price.Id, $"اعتماد السعر المرجعي {ReferencePriceContext.Label(price)} للاستخدام الحالي",
                    Lines(string.IsNullOrWhiteSpace(request.Dto.Note) ? null : $"الملاحظة: {request.Dto.Note.Trim()}",
                        ancestors.Count == 0 ? null : $"استُبدل: {string.Join("، ", ancestors.Select(a => ReferencePriceRules.Number(a.Id, a.CreatedAt)))}")));

            await ctx.NotifyAsync([price.CreatedByUserId], viewer.Id, price, NotificationType.ReferencePriceApproved, "اعتُمد سعر مرجعي أضفته",
                $"اعتمد {viewer.User.FullName} السعر {ReferencePriceContext.Label(price)} للاستخدام الحالي");
            return await ctx.DetailAsync(viewer, price.Id);
        }
    }
}
