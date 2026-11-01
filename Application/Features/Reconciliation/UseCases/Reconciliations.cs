using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Pricing;
using Application.Features.ReferencePrices;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using static Application.Features.Pricing.PricingSupport;

namespace Application.Features.Reconciliation
{
    // مطابقة الفواتير والكشوف: تسجيل المستند ← ربط بنوده بمراجع معتمدة ← مقارنة يحسبها الخادم ← المدقق يسجّل النتيجة ← غيره يعتمد أو يعيد ← إحالة المعتمد.

    public record SearchReconciliationsQuery(int? BranchId, int? Status, int? SupplierId, int? ProjectId, string? Search, int Page, int PageSize) : IRequest<PagedResultDto<ReconciliationListItemDto>>;
    public record GetReconciliationQuery(int Id) : IRequest<ReconciliationDetailDto>;
    public record GetReconciliationCandidatesQuery(int? BranchId, int SupplierId, string? Search) : IRequest<List<ReconciliationCandidateDto>>;
    public record CreateReconciliationCommand(ReconciliationRequestDto Dto) : IRequest<ReconciliationDetailDto>;
    public record UpdateReconciliationCommand(int Id, ReconciliationRequestDto Dto) : IRequest<ReconciliationDetailDto>;
    public record RecordReconciliationCommand(int Id, RecordReconciliationRequestDto Dto) : IRequest<ReconciliationDetailDto>;
    public record DecideReconciliationCommand(int Id, ReviewReconciliationRequestDto Dto) : IRequest<ReconciliationDetailDto>;
    public record ReferReconciliationCommand(int Id, ReferReconciliationRequestDto Dto) : IRequest<ReconciliationDetailDto>;
    public record CancelReconciliationCommand(int Id, PricingStepRequestDto Dto) : IRequest<ReconciliationDetailDto>;

    internal static class ReconciliationInput
    {
        public static List<ReconciliationRules.LineInput> Inputs(ReconciliationRequestDto dto) =>
            dto.Lines.Select(l => new ReconciliationRules.LineInput(l.Description, l.Unit, l.Quantity, l.UnitPrice, l.ReferenceKind, l.ReferenceId, l.Note)).ToList();

        public static string? Problem(ReconciliationRequestDto dto) =>
            ReconciliationRules.ValidateDocument(dto.DocumentKind, dto.DocumentNumber, dto.DocumentDate, dto.Currency, dto.Notes, Inputs(dto), DateTime.Today);
    }

    public class SearchReconciliationsHandler(ReconciliationContext ctx) : IRequestHandler<SearchReconciliationsQuery, PagedResultDto<ReconciliationListItemDto>>
    {
        public async Task<PagedResultDto<ReconciliationListItemDto>> Handle(SearchReconciliationsQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            ReconciliationRules.EnsureCanViewAny(viewer);
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            var branch = viewer.IsOrganizationWide ? request.BranchId : viewer.User.BranchId;
            if (!viewer.IsOrganizationWide && branch == null) return new PagedResultDto<ReconciliationListItemDto> { Items = [], TotalCount = 0, Page = page, PageSize = pageSize };
            ReconciliationStatus? status = request.Status is int s && Enum.IsDefined(typeof(ReconciliationStatus), s) ? (ReconciliationStatus)s : null;

            var (items, total) = await ctx.Reconciliations.SearchAsync(new ReconciliationFilter(branch, status, request.SupplierId, request.ProjectId, request.Search), page, pageSize);
            return new PagedResultDto<ReconciliationListItemDto> { Items = items.Select(ReconciliationSupport.ToListItem).ToList(), TotalCount = total, Page = page, PageSize = pageSize };
        }
    }

    public class GetReconciliationHandler(ReconciliationContext ctx) : IRequestHandler<GetReconciliationQuery, ReconciliationDetailDto>
    {
        public async Task<ReconciliationDetailDto> Handle(GetReconciliationQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            await ctx.LoadAsync(viewer, request.Id);
            return await ctx.DetailAsync(viewer, request.Id);
        }
    }

    /// <summary>مراجع مقترحة لمورد في الفرع: أسطر عروض مختارة من طلبات معتمدة، وأسعار مرجعية معتمدة للاستخدام الحالي — لمن يدقّق</summary>
    public class GetReconciliationCandidatesHandler(ReconciliationContext ctx) : IRequestHandler<GetReconciliationCandidatesQuery, List<ReconciliationCandidateDto>>
    {
        private const int Take = 50;

        public async Task<List<ReconciliationCandidateDto>> Handle(GetReconciliationCandidatesQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            if (!viewer.IsSuperAdmin && !viewer.Has(AppPermissions.PreparePriceReconciliation)) throw new UnauthorizedAccessException("لا تملك صلاحية تسجيل مطابقة فاتورة");
            var branch = ReconciliationContext.ResolveBranch(viewer, request.BranchId);
            if (!ReconciliationRules.InBranch(viewer, branch)) throw new UnauthorizedAccessException("هذا الفرع ليس فرعك");

            var offerLines = await ctx.Reconciliations.GetOfferLineCandidatesAsync(branch, request.SupplierId, request.Search, Take);
            var prices = await ctx.Reconciliations.GetReferencePriceCandidatesAsync(branch, request.SupplierId, request.Search, Take);
            return offerLines.Select(l => new ReconciliationCandidateDto
            {
                Kind = (int)ReconciliationReferenceKind.OfferLine, Id = l.Id, Label = PricingRules.Number(l.PricingOffer.PricingRequest.Id, l.PricingOffer.PricingRequest.CreatedAt),
                Description = l.Item.Description, Unit = l.Unit, UnitPrice = l.UnitPrice, Currency = l.PricingOffer.Currency, Quantity = l.Item.Quantity, Date = l.PricingOffer.ReceivedAt
            }).Concat(prices.Select(p => new ReconciliationCandidateDto
            {
                Kind = (int)ReconciliationReferenceKind.ReferencePrice, Id = p.Id, Label = ReferencePriceRules.Number(p.Id, p.CreatedAt), Description = p.ItemName, Unit = p.Unit,
                UnitPrice = p.UnitPrice, Currency = p.Currency, Quantity = null, Date = p.PriceDate
            })).ToList();
        }
    }

    public class CreateReconciliationHandler(ReconciliationContext ctx) : IRequestHandler<CreateReconciliationCommand, ReconciliationDetailDto>
    {
        public async Task<ReconciliationDetailDto> Handle(CreateReconciliationCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var supplier = await ctx.ResolveSupplierAsync(dto.SupplierId);
            var project = await ctx.ResolveProjectAsync(dto.ProjectId);
            var branch = ReconciliationContext.ResolveBranch(viewer, project, dto.BranchId);
            ReconciliationRules.EnsureCanPrepare(viewer, branch, supplier, project);
            if (ReconciliationInput.Problem(dto) is string problem) throw new InvalidOperationException(problem);
            var number = dto.DocumentNumber.Trim();
            var kind = (ReconciliationDocumentKind)dto.DocumentKind;
            if (await ctx.Reconciliations.DocumentExistsAsync(branch, supplier.Id, kind, number)) throw new InvalidOperationException("هذا المستند مسجَّل سابقاً لهذا المورد في الفرع");
            var contract = await ctx.ResolveContractAsync(dto.ContractId, project);
            var version = await ctx.ResolveVersionAsync(dto.DocumentVersionId, project?.Id);
            var lines = await ctx.BuildLinesAsync(dto.Lines, branch, supplier.Id);

            var created = await ctx.Reconciliations.AddAsync(new PriceReconciliation
            {
                BranchId = branch, DocumentKind = kind, DocumentNumber = number, DocumentDate = dto.DocumentDate.Date, SupplierId = supplier.Id, ProjectId = project?.Id, ContractId = contract?.Id,
                Currency = dto.Currency, Notes = (dto.Notes ?? string.Empty).Trim(), DocumentVersionId = version?.Id, Status = ReconciliationStatus.Draft, PreparedByUserId = viewer.Id, Lines = lines
            }, new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.PriceReconciliation, null,
                $"تسجيل {ReconciliationRules.KindAr(kind)} {number} من «{supplier.Name}» للمطابقة", Lines($"البنود: {lines.Count}", $"العملة: {dto.Currency}")));
            return await ctx.DetailAsync(viewer, created.Id);
        }
    }

    /// <summary>تعديل المسودة أو المعاد (لمن دقّقها): تُستبدل بنوده بالمرسلة وتُجمَّد مراجعها من جديد</summary>
    public class UpdateReconciliationHandler(ReconciliationContext ctx) : IRequestHandler<UpdateReconciliationCommand, ReconciliationDetailDto>
    {
        public async Task<ReconciliationDetailDto> Handle(UpdateReconciliationCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var rec = await ctx.LoadAsync(viewer, request.Id);
            ReconciliationRules.EnsureCanEdit(viewer, rec);
            if (ReconciliationInput.Problem(dto) is string problem) throw new InvalidOperationException(problem);

            var supplier = dto.SupplierId == rec.SupplierId ? rec.Supplier : await ctx.ResolveSupplierAsync(dto.SupplierId);
            if (supplier.Id != rec.SupplierId) Contracts.ContractRules.EnsureContractorUsable(supplier, rec.BranchId);
            var number = dto.DocumentNumber.Trim();
            var kind = (ReconciliationDocumentKind)dto.DocumentKind;
            if (await ctx.Reconciliations.DocumentExistsAsync(rec.BranchId, supplier.Id, kind, number, rec.Id)) throw new InvalidOperationException("هذا المستند مسجَّل سابقاً لهذا المورد في الفرع");
            var project = dto.ProjectId == rec.ProjectId ? rec.Project : await ctx.ResolveProjectAsync(dto.ProjectId);
            if (project != null) { Projects.ProjectRules.EnsureView(viewer, project); if (project.BranchId != rec.BranchId) throw new InvalidOperationException("المشروع من فرع آخر"); }
            var contract = await ctx.ResolveContractAsync(dto.ContractId, project);
            var version = await ctx.ResolveVersionAsync(dto.DocumentVersionId, project?.Id);
            var newLines = await ctx.BuildLinesAsync(dto.Lines, rec.BranchId, supplier.Id);

            var details = Lines(Change("الرقم", rec.DocumentNumber, number), Change("التاريخ", rec.DocumentDate.ToString("yyyy-MM-dd"), dto.DocumentDate.ToString("yyyy-MM-dd")),
                Change("المورد", rec.Supplier.Name, supplier.Name), Change("العملة", rec.Currency, dto.Currency), $"البنود: {newLines.Count}");

            var removed = rec.Lines.ToList();
            rec.DocumentKind = kind; rec.DocumentNumber = number; rec.DocumentDate = dto.DocumentDate.Date; rec.SupplierId = supplier.Id; rec.ProjectId = project?.Id; rec.ContractId = contract?.Id;
            rec.Currency = dto.Currency; rec.Notes = (dto.Notes ?? string.Empty).Trim(); rec.DocumentVersionId = version?.Id;
            foreach (var line in newLines) { line.PriceReconciliationId = rec.Id; rec.Lines.Add(line); }

            await ctx.Reconciliations.SaveAsync(rec, removed, dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.PriceReconciliation, rec.Id, $"تعديل المطابقة {ReconciliationContext.Label(rec)}", details));
            return await ctx.DetailAsync(viewer, rec.Id);
        }
    }

    /// <summary>تسجيل نتيجة التدقيق (إحدى ست) بملاحظات المدقق؛ وينبَّه حاملو صلاحية الاعتماد</summary>
    public class RecordReconciliationHandler(ReconciliationContext ctx) : IRequestHandler<RecordReconciliationCommand, ReconciliationDetailDto>
    {
        public async Task<ReconciliationDetailDto> Handle(RecordReconciliationCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var rec = await ctx.LoadAsync(viewer, request.Id);
            var result = (ReconciliationStatus)dto.Result;
            ReconciliationRules.EnsureCanRecord(viewer, rec, result, dto.Note);

            rec.Status = result;
            rec.AuditResult = result;
            rec.AuditNote = (dto.Note ?? string.Empty).Trim();
            rec.AuditedAt = DateTime.UtcNow;
            rec.DecisionNote = string.Empty;
            await ctx.Reconciliations.SaveAsync(rec, [], dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Submitted, AuditEntities.PriceReconciliation, rec.Id, $"تسجيل نتيجة تدقيق «{ReconciliationRules.StatusAr(result)}» للمطابقة {ReconciliationContext.Label(rec)}",
                    Lines($"المقارنة تقترح: {ReconciliationRules.StatusAr(ReconciliationRules.Suggest(rec))}", string.IsNullOrWhiteSpace(dto.Note) ? null : $"الملاحظات: {dto.Note.Trim()}")));

            await ctx.NotifyAsync(await ctx.HoldersAsync(rec.BranchId, AppPermissions.ApprovePriceReconciliation), viewer.Id, rec, NotificationType.ReconciliationAwaiting,
                "نتيجة مطابقة بانتظار اعتمادك", $"سجّل {viewer.User.FullName} نتيجة «{ReconciliationRules.StatusAr(result)}» للمطابقة {ReconciliationContext.Label(rec)}");
            return await ctx.DetailAsync(viewer, rec.Id);
        }
    }

    /// <summary>الاعتماد («معتمد بعد التدقيق») أو الإعادة («معاد للتصحيح» بسبب) — لغير من دقّق، حتى مدير النظام</summary>
    public class DecideReconciliationHandler(ReconciliationContext ctx) : IRequestHandler<DecideReconciliationCommand, ReconciliationDetailDto>
    {
        public async Task<ReconciliationDetailDto> Handle(DecideReconciliationCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var rec = await ctx.LoadAsync(viewer, request.Id);
            ReconciliationRules.EnsureCanDecide(viewer, rec, dto.Approve, dto.Note);

            rec.Status = dto.Approve ? ReconciliationStatus.ApprovedAfterAudit : ReconciliationStatus.ReturnedForCorrection;
            rec.DecidedByUserId = viewer.Id;
            rec.DecidedAt = DateTime.UtcNow;
            rec.DecisionNote = (dto.Note ?? string.Empty).Trim();
            await ctx.Reconciliations.SaveAsync(rec, [], dto.RowVersion,
                new AuditEntry(viewer.Id, dto.Approve ? AuditActions.Approved : AuditActions.Returned, AuditEntities.PriceReconciliation, rec.Id,
                    $"{(dto.Approve ? "اعتماد" : "إعادة للتصحيح")} المطابقة {ReconciliationContext.Label(rec)}", string.IsNullOrWhiteSpace(dto.Note) ? string.Empty : $"الملاحظة: {dto.Note.Trim()}"));

            await ctx.NotifyAsync([rec.PreparedByUserId], viewer.Id, rec, NotificationType.ReconciliationDecided, dto.Approve ? "اعتُمدت مطابقة دقّقتها" : "أُعيدت إليك مطابقة للتصحيح",
                $"{(dto.Approve ? "اعتمد" : "أعاد")} {viewer.User.FullName} المطابقة {ReconciliationContext.Label(rec)}" + (dto.Approve || string.IsNullOrWhiteSpace(dto.Note) ? string.Empty : $": {dto.Note.Trim()}"));
            return await ctx.DetailAsync(viewer, rec.Id);
        }
    }

    /// <summary>الإحالة إلى الجهة المختصة بعد الاعتماد: تسجيل يدوي بالجهة والتاريخ (لا ربط إلكتروني)</summary>
    public class ReferReconciliationHandler(ReconciliationContext ctx) : IRequestHandler<ReferReconciliationCommand, ReconciliationDetailDto>
    {
        public async Task<ReconciliationDetailDto> Handle(ReferReconciliationCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var rec = await ctx.LoadAsync(viewer, request.Id);
            ReconciliationRules.EnsureCanRefer(viewer, rec, dto.Recipient);

            rec.ReferredTo = dto.Recipient.Trim();
            rec.ReferralNote = (dto.Note ?? string.Empty).Trim();
            rec.ReferredAt = DateTime.UtcNow;
            rec.ReferredByUserId = viewer.Id;
            await ctx.Reconciliations.SaveAsync(rec, [], dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Sent, AuditEntities.PriceReconciliation, rec.Id, $"إحالة المطابقة {ReconciliationContext.Label(rec)} إلى {rec.ReferredTo}",
                    string.IsNullOrWhiteSpace(dto.Note) ? string.Empty : $"الملاحظة: {dto.Note.Trim()}"));
            return await ctx.DetailAsync(viewer, rec.Id);
        }
    }

    public class CancelReconciliationHandler(ReconciliationContext ctx) : IRequestHandler<CancelReconciliationCommand, ReconciliationDetailDto>
    {
        public async Task<ReconciliationDetailDto> Handle(CancelReconciliationCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var rec = await ctx.LoadAsync(viewer, request.Id);
            ReconciliationRules.EnsureCanCancel(viewer, rec);

            rec.Status = ReconciliationStatus.Cancelled;
            await ctx.Reconciliations.SaveAsync(rec, [], request.Dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Cancelled, AuditEntities.PriceReconciliation, rec.Id, $"إلغاء المطابقة {ReconciliationContext.Label(rec)}",
                    string.IsNullOrWhiteSpace(request.Dto.Note) ? string.Empty : $"السبب: {request.Dto.Note.Trim()}"));
            return await ctx.DetailAsync(viewer, rec.Id);
        }
    }
}
