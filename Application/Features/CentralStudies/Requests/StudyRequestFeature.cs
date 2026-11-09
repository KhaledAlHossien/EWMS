using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using FluentValidation;
using MediatR;

namespace Application.Features.CentralStudies.Requests
{
    // طلب تجهيز دراسة تكاليف — جذر السلسلة. حدّ كل الصلاحيات: طلبات فرعه (والمؤسسة كلها لنظرة المؤسسة).
    // التسجيل النهائي (Submit) يجعله معتمداً مباشرة (لا تواقيع على الطلب)، فيُصدَر له تكليف.
    public record SearchStudyRequestsQuery(StudyDocumentFilterDto Filter) : IRequest<PagedResultDto<StudyRequestDto>>;
    public record GetStudyRequestQuery(int Id) : IRequest<StudyRequestDto>;
    public record CreateStudyRequestCommand(StudyRequestFormDto Dto) : IRequest<StudyRequestDto>;
    public record UpdateStudyRequestCommand(int Id, StudyRequestFormDto Dto) : IRequest<StudyRequestDto>;
    public record DeleteStudyRequestCommand(int Id) : IRequest<Unit>;
    public record SubmitStudyRequestCommand(int Id) : IRequest<StudyRequestDto>;
    public record CancelStudyRequestCommand(int Id, string Reason) : IRequest<StudyRequestDto>;

    // ════════════════════ التحقق ════════════════════

    public class StudyRequestFormValidator : AbstractValidator<StudyRequestFormDto>
    {
        public StudyRequestFormValidator()
        {
            RuleFor(x => x.RequestDate).NotEmpty().WithMessage("تاريخ الطلب مطلوب");
            RuleFor(x => x.Urgency).Must(v => Enum.IsDefined(typeof(StudyUrgency), v)).WithMessage("حدد الاستعجال");
            RuleFor(x => x.WorkType).Must(v => Enum.IsDefined(typeof(StudyWorkType), v)).WithMessage("حدد نوع العمل");
            RuleFor(x => x.MainBeneficiary).NotEmpty().WithMessage("الجهة المستفيدة الرئيسية مطلوبة")
                .MaximumLength(200).WithMessage("الجهة المستفيدة لا تتجاوز 200 حرف");
            RuleFor(x => x.SubBeneficiary).MaximumLength(200).WithMessage("اسم الجهة الفرعية لا يتجاوز 200 حرف");
            RuleFor(x => x.GovernorateCode).Must(Governorates.IsValidCode).WithMessage("اختر المحافظة من القائمة");
            RuleFor(x => x.DiwanRequestNumber).MaximumLength(50).WithMessage("رقم الديوان لا يتجاوز 50 حرفاً");
            RuleFor(x => x.MinistryArchiveNumber).MaximumLength(50).WithMessage("رقم أرشيف الوزارة لا يتجاوز 50 حرفاً");
            RuleFor(x => x.AdminArchiveNumber).MaximumLength(50).WithMessage("رقم أرشيف الإدارة لا يتجاوز 50 حرفاً");
            RuleFor(x => x.BeneficiaryArchiveNumber).MaximumLength(50).WithMessage("رقم أرشيف الجهة لا يتجاوز 50 حرفاً");
            RuleFor(x => x.EntityCode).MaximumLength(30).WithMessage("رمز الجهة لا يتجاوز 30 حرفاً");
            RuleFor(x => x.Area).MaximumLength(100).WithMessage("المنطقة لا تتجاوز 100 حرف");
            RuleFor(x => x.CityOrVillage).MaximumLength(100).WithMessage("المدينة أو القرية لا تتجاوز 100 حرف");
            RuleFor(x => x.PropertyNature).MaximumLength(100).WithMessage("طبيعة العقار لا تتجاوز 100 حرف");
            RuleFor(x => x.FollowerName).MaximumLength(150).WithMessage("اسم المتابع لا يتجاوز 150 حرفاً");
            RuleFor(x => x.FollowerPhone).Must(p => string.IsNullOrWhiteSpace(p) || PhoneRules.IsValid(p))
                .WithMessage("رقم تواصل المتابع غير صحيح (رقم سوري)");
            RuleFor(x => x.Notes).MaximumLength(2000).WithMessage("الملاحظات لا تتجاوز 2000 حرف");
            RuleFor(x => x.SiteId).GreaterThan(0).When(x => x.SiteId != null).WithMessage("الموقع غير صالح");
        }
    }

    public class CreateStudyRequestValidator : AbstractValidator<CreateStudyRequestCommand>
    {
        public CreateStudyRequestValidator() => RuleFor(x => x.Dto).SetValidator(new StudyRequestFormValidator());
    }

    public class UpdateStudyRequestValidator : AbstractValidator<UpdateStudyRequestCommand>
    {
        public UpdateStudyRequestValidator() => RuleFor(x => x.Dto).SetValidator(new StudyRequestFormValidator());
    }

    public class CancelStudyRequestValidator : AbstractValidator<CancelStudyRequestCommand>
    {
        public CancelStudyRequestValidator() =>
            RuleFor(x => x.Reason).NotEmpty().WithMessage("اذكر سبب الإلغاء").MaximumLength(1000).WithMessage("السبب لا يتجاوز 1000 حرف");
    }

    // ════════════════════ المعالج ════════════════════

    public class StudyRequestHandler :
        IRequestHandler<SearchStudyRequestsQuery, PagedResultDto<StudyRequestDto>>,
        IRequestHandler<GetStudyRequestQuery, StudyRequestDto>,
        IRequestHandler<CreateStudyRequestCommand, StudyRequestDto>,
        IRequestHandler<UpdateStudyRequestCommand, StudyRequestDto>,
        IRequestHandler<DeleteStudyRequestCommand, Unit>,
        IRequestHandler<SubmitStudyRequestCommand, StudyRequestDto>,
        IRequestHandler<CancelStudyRequestCommand, StudyRequestDto>
    {
        private readonly IStudyRequestService _service;
        private readonly IStudyNumberService _numbers;
        private readonly ISiteService _sites;
        private readonly IBranchService _branches;
        private readonly StudyKit _kit;

        public StudyRequestHandler(IStudyRequestService service, IStudyNumberService numbers, ISiteService sites,
            IBranchService branches, StudyKit kit)
        {
            _service = service;
            _numbers = numbers;
            _sites = sites;
            _branches = branches;
            _kit = kit;
        }

        private async Task<StudyRequest> LoadAsync(int id, Viewer viewer)
        {
            var entity = await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("الطلب غير موجود");
            StudyRules.EnsureInScope(viewer, entity.BranchId);
            return entity;
        }

        private StudyRequestDto ToDto(StudyRequest entity, Viewer viewer)
        {
            var dto = _kit.Mapper.Map<StudyRequestDto>(entity);
            var inScope = StudyRules.InScope(viewer, entity.BranchId);
            dto.CanEdit = inScope && viewer.Has("EditStudyRequest") && StudyRules.IsEditable(entity.Status);
            dto.CanSubmit = dto.CanEdit;
            dto.CanDelete = inScope && viewer.Has("DeleteStudyRequest") && entity.Status == StudyDocumentStatus.Draft;
            dto.CanCancel = inScope && viewer.Has(StudyPermissions.CancelDocuments)
                            && entity.Status != StudyDocumentStatus.Cancelled && entity.Status != StudyDocumentStatus.Draft
                            && (entity.Assignment == null || entity.Assignment.Status == StudyDocumentStatus.Cancelled);
            dto.CanCreateAssignment = inScope && viewer.Has("CreateStudyAssignment")
                                      && entity.Status == StudyDocumentStatus.Approved && entity.Assignment == null;
            return dto;
        }

        private async Task<StudyRequestDto> DetailAsync(int id, Viewer viewer)
        {
            var entity = await LoadAsync(id, viewer);
            var dto = ToDto(entity, viewer);
            dto.Chain = await _kit.ChainAsync(entity.Id);
            await _kit.AddWhoIsNextAsync(dto, StudyEntityType.Request, entity.Id);
            return dto;
        }

        private async Task EnsureSiteAsync(int? siteId)
        {
            if (siteId is int id && !await _sites.ExistsAsync(id))
                throw new KeyNotFoundException("الموقع المحدد غير موجود في سجل المواقع");
        }

        private static void Normalize(StudyRequest e)
        {
            e.MainBeneficiary = e.MainBeneficiary.Trim();
            e.SubBeneficiary = Clean(e.SubBeneficiary);
            e.DiwanRequestNumber = Clean(e.DiwanRequestNumber);
            e.MinistryArchiveNumber = Clean(e.MinistryArchiveNumber);
            e.AdminArchiveNumber = Clean(e.AdminArchiveNumber);
            e.BeneficiaryArchiveNumber = Clean(e.BeneficiaryArchiveNumber);
            e.EntityCode = Clean(e.EntityCode);
            e.Area = Clean(e.Area);
            e.CityOrVillage = Clean(e.CityOrVillage);
            e.PropertyNature = Clean(e.PropertyNature);
            e.FollowerName = Clean(e.FollowerName);
            e.FollowerPhone = PhoneRules.ToStored(e.FollowerPhone);
            e.Notes = Clean(e.Notes);
            e.GovernorateCode = e.GovernorateCode.Trim().ToUpperInvariant();
            e.MinistryArchiveDate = e.MinistryArchiveDate?.Date;
            e.AdminArchiveDate = e.AdminArchiveDate?.Date;
            e.AddedDate = e.AddedDate?.Date;
        }

        internal static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        public async Task<PagedResultDto<StudyRequestDto>> Handle(SearchStudyRequestsQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var (page, size) = StudyRules.NormalizePaging(request.Filter.Page, request.Filter.PageSize);
            var (items, total) = await _service.SearchAsync(request.Filter, StudyRules.BranchLimit(viewer), page, size);
            return new PagedResultDto<StudyRequestDto>
            {
                Items = items.Select(x => ToDto(x, viewer)).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = size
            };
        }

        public async Task<StudyRequestDto> Handle(GetStudyRequestQuery request, CancellationToken ct) =>
            await DetailAsync(request.Id, await _kit.ViewerAsync());

        public async Task<StudyRequestDto> Handle(CreateStudyRequestCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            // الطلب يتبع فرع منشئه؛ نظرة المؤسسة تختار الفرع
            var branchId = viewer.IsOrganizationWide && request.Dto.BranchId is int chosen ? chosen : viewer.User.BranchId
                ?? throw new InvalidOperationException("حسابك غير مرتبط بفرع — لا يمكنك تسجيل طلبات دراسة");
            if (viewer.IsOrganizationWide && !await _branches.ExistsAsync(branchId))
                throw new KeyNotFoundException("الفرع المحدد غير موجود");
            await EnsureSiteAsync(request.Dto.SiteId);

            var entity = _kit.Mapper.Map<StudyRequest>(request.Dto);
            Normalize(entity);
            var number = await _numbers.NextAsync(StudyDocumentType.Request, DateTime.Today.Year);
            entity.RequestNumber = StudyRules.FormatNumber(StudyDocumentType.Request, DateTime.Today.Year, number);
            entity.BranchId = branchId;
            entity.CreatedByUserId = viewer.Id;
            entity.Status = StudyDocumentStatus.Draft;
            entity.CreatedAt = DateTime.Now;

            await _service.AddAsync(entity);
            await _kit.LogAsync(entity.Id, StudyEntityType.Request, entity.Id, StudyLogAction.Created,
                $"سجّل طلب تجهيز الدراسة {entity.RequestNumber}", entity.MainBeneficiary);
            return await DetailAsync(entity.Id, viewer);
        }

        public async Task<StudyRequestDto> Handle(UpdateStudyRequestCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await LoadAsync(request.Id, viewer);
            StudyRules.EnsureEditable(entity.Status);
            await EnsureSiteAsync(request.Dto.SiteId);

            _kit.Mapper.Map(request.Dto, entity);
            Normalize(entity);
            entity.Status = StudyDocumentStatus.Draft;
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity, request.Dto.RowVersion);
            await _kit.LogAsync(entity.Id, StudyEntityType.Request, entity.Id, StudyLogAction.Edited,
                $"عدّل الطلب {entity.RequestNumber}");
            return await DetailAsync(entity.Id, viewer);
        }

        public async Task<Unit> Handle(DeleteStudyRequestCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await LoadAsync(request.Id, viewer);
            StudyRules.EnsureDraftForDelete(entity.Status);
            if (entity.Assignment != null)
                throw new InvalidOperationException("للطلب تكليف داخلي — لا يُحذف");
            await _service.DeleteAsync(entity);   // سجل الدراسة ومرفقات الطلب تُحذف معه
            return Unit.Value;
        }

        public async Task<StudyRequestDto> Handle(SubmitStudyRequestCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await LoadAsync(request.Id, viewer);
            StudyRules.EnsureEditable(entity.Status);
            entity.Status = StudyDocumentStatus.Approved;
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity);
            await _kit.LogAsync(entity.Id, StudyEntityType.Request, entity.Id, StudyLogAction.Approved,
                $"سُجّل الطلب {entity.RequestNumber} نهائياً");
            await _kit.NotifyNextStepAsync(StudyEntityType.Request, entity.Id);   // لمن يصدر التكليف
            return await DetailAsync(entity.Id, viewer);
        }

        public async Task<StudyRequestDto> Handle(CancelStudyRequestCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await LoadAsync(request.Id, viewer);
            if (entity.Status == StudyDocumentStatus.Cancelled)
                throw new InvalidOperationException("الطلب ملغى مسبقاً");
            if (entity.Status == StudyDocumentStatus.Draft)
                throw new InvalidOperationException("المسودة تُحذف ولا تُلغى");
            if (entity.Assignment != null && entity.Assignment.Status != StudyDocumentStatus.Cancelled)
                throw new InvalidOperationException("للطلب تكليف قائم — ألغِ التكليف أولاً");

            entity.Status = StudyDocumentStatus.Cancelled;
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity);
            await _kit.LogAsync(entity.Id, StudyEntityType.Request, entity.Id, StudyLogAction.Cancelled,
                $"ألغى الطلب {entity.RequestNumber}", request.Reason.Trim());
            return await DetailAsync(entity.Id, viewer);
        }
    }
}
