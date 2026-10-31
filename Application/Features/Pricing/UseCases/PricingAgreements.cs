using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using static Application.Features.Pricing.PricingSupport;

namespace Application.Features.Pricing
{
    // محاضر الاتفاق مع المتعهدين الجدد: الإعداد لمدير المكتب وحده (صلاحية مستقلة)، والإرسال، ثم اعتماد غير مُعِدّه أو إعادته للتصحيح. المعتمد لا يُعدَّل.

    public record SearchPricingAgreementsQuery(int? BranchId, int? Status, int? SupplierId, string? Search, int Page, int PageSize) : IRequest<PagedResultDto<PricingAgreementListItemDto>>;
    public record GetPricingAgreementQuery(int Id) : IRequest<PricingAgreementDetailDto>;
    public record CreatePricingAgreementCommand(PricingAgreementRequestDto Dto) : IRequest<PricingAgreementDetailDto>;
    public record UpdatePricingAgreementCommand(int Id, PricingAgreementRequestDto Dto) : IRequest<PricingAgreementDetailDto>;
    public record SubmitPricingAgreementCommand(int Id, PricingStepRequestDto Dto) : IRequest<PricingAgreementDetailDto>;
    public record ReviewPricingAgreementCommand(int Id, ReviewPricingAgreementRequestDto Dto) : IRequest<PricingAgreementDetailDto>;
    public record CancelPricingAgreementCommand(int Id, PricingStepRequestDto Dto) : IRequest<PricingAgreementDetailDto>;

    internal static class AgreementInput
    {
        public static List<PricingRules.AgreementLineInput> Inputs(PricingAgreementRequestDto dto) =>
            dto.Lines.Select(l => new PricingRules.AgreementLineInput(l.Description, l.Quantity, l.Unit, l.UnitPrice)).ToList();

        public static List<PricingAgreementLine> NewLines(PricingAgreementRequestDto dto) =>
            dto.Lines.Select((l, i) => new PricingAgreementLine { SortOrder = i + 1, Description = l.Description.Trim(), Quantity = l.Quantity, Unit = l.Unit.Trim(), UnitPrice = l.UnitPrice }).ToList();

        public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        /// <summary>الطلب والعقد والمشروع المرتبطة بالمحضر بعد التحقق: من فرع المحضر ويراها المستخدم</summary>
        public static async Task<(Project? Project, Contract? Contract, PricingRequest? Request)> LinksAsync(PricingContext ctx, Viewer viewer, PricingAgreementRequestDto dto, int branchId)
        {
            var project = dto.ProjectId is int pid ? await ctx.Projects.GetByIdAsync(pid) ?? throw new KeyNotFoundException("المشروع غير موجود") : null;
            var contract = dto.ContractId is int cid ? await ctx.Contracts.GetByIdAsync(cid) ?? throw new KeyNotFoundException("العقد غير موجود") : null;
            if (contract != null)
            {
                if (project == null || contract.ProjectId != project.Id) throw new InvalidOperationException("العقد من مشروع آخر: اختر مشروعه");
            }
            PricingRequest? pricing = null;
            if (dto.PricingRequestId is int rid)
            {
                pricing = await ctx.Pricing.GetByIdAsync(rid) ?? throw new KeyNotFoundException("طلب التسعير غير موجود");
                PricingRules.EnsureCanView(viewer, pricing);
                if (pricing.BranchId != branchId) throw new InvalidOperationException("طلب التسعير من فرع آخر");
            }
            return (project, contract, pricing);
        }
    }

    public class SearchPricingAgreementsHandler(PricingContext ctx) : IRequestHandler<SearchPricingAgreementsQuery, PagedResultDto<PricingAgreementListItemDto>>
    {
        public async Task<PagedResultDto<PricingAgreementListItemDto>> Handle(SearchPricingAgreementsQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            if (!AppPermissions.AnyOf["AnyPricingAgreementView"].Any(viewer.Has) && !viewer.IsSuperAdmin)
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض محاضر الاتفاق");
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);

            var branch = viewer.IsOrganizationWide ? request.BranchId : viewer.User.BranchId;
            if (!viewer.IsOrganizationWide && branch == null) return new PagedResultDto<PricingAgreementListItemDto> { Items = [], TotalCount = 0, Page = page, PageSize = pageSize };
            AgreementStatus? status = request.Status is int s && Enum.IsDefined(typeof(AgreementStatus), s) ? (AgreementStatus)s : null;

            var (items, total) = await ctx.Pricing.SearchAgreementsAsync(new AgreementFilter(branch, status, request.SupplierId, request.Search), page, pageSize);
            return new PagedResultDto<PricingAgreementListItemDto> { Items = items.Select(ToAgreementListItem).ToList(), TotalCount = total, Page = page, PageSize = pageSize };
        }
    }

    public class GetPricingAgreementHandler(PricingContext ctx) : IRequestHandler<GetPricingAgreementQuery, PricingAgreementDetailDto>
    {
        public async Task<PricingAgreementDetailDto> Handle(GetPricingAgreementQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var agreement = await ctx.LoadAgreementAsync(viewer, request.Id);
            return ToAgreementDetail(agreement, viewer);
        }
    }

    public class CreatePricingAgreementHandler(PricingContext ctx) : IRequestHandler<CreatePricingAgreementCommand, PricingAgreementDetailDto>
    {
        public async Task<PricingAgreementDetailDto> Handle(CreatePricingAgreementCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var supplier = await ctx.ResolveSupplierAsync(dto.SupplierId);
            var project = dto.ProjectId is int pid ? await ctx.Projects.GetByIdAsync(pid) ?? throw new KeyNotFoundException("المشروع غير موجود") : null;
            var branch = PricingContext.ResolveBranch(viewer, project, dto.BranchId);
            PricingRules.EnsureCanPrepareAgreement(viewer, branch, supplier, project);
            if (PricingRules.ValidateAgreement(dto.Number, dto.AgreementDate, dto.TechnicalSpecs, dto.ExecutionMethod, dto.Currency, dto.Notes, AgreementInput.Inputs(dto), DateTime.Today) is string problem)
                throw new InvalidOperationException(problem);
            var number = AgreementInput.Clean(dto.Number);
            if (number != null && await ctx.Pricing.AgreementNumberExistsAsync(branch, number)) throw new InvalidOperationException("رقم المحضر مستعمل في الفرع");
            var (_, contract, pricing) = await AgreementInput.LinksAsync(ctx, viewer, dto, branch);
            var version = await ctx.ResolveVersionAsync(dto.DocumentVersionId, project?.Id);

            var created = await ctx.Pricing.AddAgreementAsync(new PricingAgreement
            {
                BranchId = branch, Number = number, AgreementDate = dto.AgreementDate.Date, SupplierId = supplier.Id, ProjectId = project?.Id, ContractId = contract?.Id,
                PricingRequestId = pricing?.Id, TechnicalSpecs = (dto.TechnicalSpecs ?? string.Empty).Trim(), ExecutionMethod = (dto.ExecutionMethod ?? string.Empty).Trim(),
                Currency = dto.Currency, Notes = (dto.Notes ?? string.Empty).Trim(), DocumentVersionId = version?.Id, Status = AgreementStatus.Draft,
                PreparedByUserId = viewer.Id, Lines = AgreementInput.NewLines(dto)
            }, new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.PricingAgreement, null, $"إعداد محضر اتفاق مع «{supplier.Name}»",
                Lines(number is null ? null : $"الرقم: {number}", $"البنود: {dto.Lines.Count}")));
            return await ctx.AgreementDetailAsync(viewer, created.Id);
        }
    }

    /// <summary>تعديل المسودة أو المعاد (لمن أعدّه): تُستبدل بنوده بالقائمة المرسلة</summary>
    public class UpdatePricingAgreementHandler(PricingContext ctx) : IRequestHandler<UpdatePricingAgreementCommand, PricingAgreementDetailDto>
    {
        public async Task<PricingAgreementDetailDto> Handle(UpdatePricingAgreementCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var agreement = await ctx.LoadAgreementAsync(viewer, request.Id);
            PricingRules.EnsureCanEditAgreement(viewer, agreement);
            if (PricingRules.ValidateAgreement(dto.Number, dto.AgreementDate, dto.TechnicalSpecs, dto.ExecutionMethod, dto.Currency, dto.Notes, AgreementInput.Inputs(dto), DateTime.Today) is string problem)
                throw new InvalidOperationException(problem);

            var supplier = dto.SupplierId == agreement.SupplierId ? agreement.Supplier : await ctx.ResolveSupplierAsync(dto.SupplierId);
            if (supplier.Id != agreement.SupplierId) Contracts.ContractRules.EnsureContractorUsable(supplier, agreement.BranchId);
            var number = AgreementInput.Clean(dto.Number);
            if (number != null && await ctx.Pricing.AgreementNumberExistsAsync(agreement.BranchId, number, agreement.Id)) throw new InvalidOperationException("رقم المحضر مستعمل في الفرع");
            var (project, contract, pricing) = await AgreementInput.LinksAsync(ctx, viewer, dto, agreement.BranchId);
            if (project != null && project.BranchId != agreement.BranchId) throw new InvalidOperationException("المشروع من فرع آخر");
            var version = await ctx.ResolveVersionAsync(dto.DocumentVersionId, project?.Id);

            var details = Lines(
                Change("الرقم", agreement.Number, number), Change("التاريخ", agreement.AgreementDate.ToString("yyyy-MM-dd"), dto.AgreementDate.ToString("yyyy-MM-dd")),
                Change("المتعهد", agreement.Supplier.Name, supplier.Name), Change("المواصفات", agreement.TechnicalSpecs, dto.TechnicalSpecs),
                Change("طريقة التنفيذ", agreement.ExecutionMethod, dto.ExecutionMethod), Change("العملة", agreement.Currency, dto.Currency), $"البنود: {dto.Lines.Count}");

            var removed = agreement.Lines.ToList();
            agreement.Number = number;
            agreement.AgreementDate = dto.AgreementDate.Date;
            agreement.SupplierId = supplier.Id;
            agreement.ProjectId = project?.Id;
            agreement.ContractId = contract?.Id;
            agreement.PricingRequestId = pricing?.Id;
            agreement.TechnicalSpecs = (dto.TechnicalSpecs ?? string.Empty).Trim();
            agreement.ExecutionMethod = (dto.ExecutionMethod ?? string.Empty).Trim();
            agreement.Currency = dto.Currency;
            agreement.Notes = (dto.Notes ?? string.Empty).Trim();
            agreement.DocumentVersionId = version?.Id;
            foreach (var line in AgreementInput.NewLines(dto)) { line.PricingAgreementId = agreement.Id; agreement.Lines.Add(line); }

            await ctx.Pricing.SaveAgreementAsync(agreement, removed, dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.PricingAgreement, agreement.Id, $"تعديل محضر الاتفاق {PricingContext.Label(agreement)}", details));
            return await ctx.AgreementDetailAsync(viewer, agreement.Id);
        }
    }

    /// <summary>الإرسال للاعتماد: يلزم الرقم الرسمي وبند مسعَّر؛ ويُنبَّه حاملو صلاحية الاعتماد</summary>
    public class SubmitPricingAgreementHandler(PricingContext ctx) : IRequestHandler<SubmitPricingAgreementCommand, PricingAgreementDetailDto>
    {
        public async Task<PricingAgreementDetailDto> Handle(SubmitPricingAgreementCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var agreement = await ctx.LoadAgreementAsync(viewer, request.Id);
            PricingRules.EnsureCanSubmitAgreement(viewer, agreement);

            agreement.Status = AgreementStatus.Submitted;
            agreement.SubmittedAt = DateTime.UtcNow;
            agreement.ReturnNote = string.Empty;
            await ctx.Pricing.SaveAgreementAsync(agreement, [], request.Dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Submitted, AuditEntities.PricingAgreement, agreement.Id, $"إرسال محضر الاتفاق {PricingContext.Label(agreement)} للاعتماد"));

            await ctx.NotifyAsync(await ctx.HoldersAsync(agreement.BranchId, AppPermissions.ApprovePricingAgreement), viewer.Id, AuditEntities.PricingAgreement, agreement.Id,
                NotificationType.PricingAgreementAwaiting, "محضر اتفاق بانتظار اعتمادك", $"أرسل {viewer.User.FullName} المحضر {PricingContext.Label(agreement)}");
            return await ctx.AgreementDetailAsync(viewer, agreement.Id);
        }
    }

    /// <summary>الاعتماد (غير من أعدّه، حتى مدير النظام) أو الإعادة للتصحيح بسبب مكتوب</summary>
    public class ReviewPricingAgreementHandler(PricingContext ctx) : IRequestHandler<ReviewPricingAgreementCommand, PricingAgreementDetailDto>
    {
        public async Task<PricingAgreementDetailDto> Handle(ReviewPricingAgreementCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var agreement = await ctx.LoadAgreementAsync(viewer, request.Id);
            PricingRules.EnsureCanApproveAgreement(viewer, agreement, dto.Approve, dto.Note);

            if (dto.Approve)
            {
                agreement.Status = AgreementStatus.Approved;
                agreement.ApprovedByUserId = viewer.Id;
                agreement.ApprovedAt = DateTime.UtcNow;
                agreement.ApprovalNote = (dto.Note ?? string.Empty).Trim();
            }
            else
            {
                agreement.Status = AgreementStatus.Returned;
                agreement.ReturnNote = dto.Note!.Trim();
                agreement.SubmittedAt = null;
            }
            await ctx.Pricing.SaveAgreementAsync(agreement, [], dto.RowVersion,
                new AuditEntry(viewer.Id, dto.Approve ? AuditActions.Approved : AuditActions.Returned, AuditEntities.PricingAgreement, agreement.Id,
                    $"{(dto.Approve ? "اعتماد" : "إعادة")} محضر الاتفاق {PricingContext.Label(agreement)}", string.IsNullOrWhiteSpace(dto.Note) ? string.Empty : $"الملاحظة: {dto.Note.Trim()}"));

            await ctx.NotifyAsync([agreement.PreparedByUserId], viewer.Id, AuditEntities.PricingAgreement, agreement.Id, NotificationType.PricingAgreementDecided,
                dto.Approve ? "اعتُمد محضر أعددته" : "أُعيد إليك محضر اتفاق",
                $"{(dto.Approve ? "اعتمد" : "أعاد")} {viewer.User.FullName} المحضر {PricingContext.Label(agreement)}" + (dto.Approve || string.IsNullOrWhiteSpace(dto.Note) ? string.Empty : $": {dto.Note.Trim()}"));
            return await ctx.AgreementDetailAsync(viewer, agreement.Id);
        }
    }

    public class CancelPricingAgreementHandler(PricingContext ctx) : IRequestHandler<CancelPricingAgreementCommand, PricingAgreementDetailDto>
    {
        public async Task<PricingAgreementDetailDto> Handle(CancelPricingAgreementCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var agreement = await ctx.LoadAgreementAsync(viewer, request.Id);
            PricingRules.EnsureCanCancelAgreement(viewer, agreement);

            agreement.Status = AgreementStatus.Cancelled;
            await ctx.Pricing.SaveAgreementAsync(agreement, [], request.Dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Cancelled, AuditEntities.PricingAgreement, agreement.Id, $"إلغاء محضر الاتفاق {PricingContext.Label(agreement)}",
                    string.IsNullOrWhiteSpace(request.Dto.Note) ? string.Empty : $"السبب: {request.Dto.Note.Trim()}"));
            return await ctx.AgreementDetailAsync(viewer, agreement.Id);
        }
    }
}
