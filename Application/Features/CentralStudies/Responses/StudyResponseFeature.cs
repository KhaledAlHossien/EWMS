using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.CentralStudies.Requests;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using FluentValidation;
using MediatR;

namespace Application.Features.CentralStudies.Responses
{
    // إشعار الرد على التكليف — رد واحد لكل تكليف معتمد ولا يُعاد. يوقّعه القسم المتابع ثم التخطيط وإدارة المشاريع.
    // أجزاؤه تُزامن مع النموذج في كل حفظ (المباني بنوعها، الكادر باختصاصه، والبنود تُستبدل).
    public record SearchStudyResponsesQuery(StudyDocumentFilterDto Filter) : IRequest<PagedResultDto<StudyResponseDto>>;
    public record GetStudyResponseQuery(int Id) : IRequest<StudyResponseDto>;
    public record CreateStudyResponseCommand(StudyResponseFormDto Dto) : IRequest<StudyResponseDto>;
    public record UpdateStudyResponseCommand(int Id, StudyResponseFormDto Dto) : IRequest<StudyResponseDto>;
    public record DeleteStudyResponseCommand(int Id) : IRequest<Unit>;
    public record SubmitStudyResponseCommand(int Id) : IRequest<StudyResponseDto>;
    public record CancelStudyResponseCommand(int Id, string Reason) : IRequest<StudyResponseDto>;

    // ════════════════════ التحقق ════════════════════

    public class StudyResponseFormValidator : AbstractValidator<StudyResponseFormDto>
    {
        public const int MaxItems = 200;

        public StudyResponseFormValidator()
        {
            RuleFor(x => x.ResponseDate).NotEmpty().WithMessage("تاريخ الرد مطلوب");
            RuleFor(x => x.Recommendation).Must(v => Enum.IsDefined(typeof(StudyRecommendation), v)).WithMessage("حدد التوصية الفنية النهائية");
            RuleFor(x => x.RequiredDays).InclusiveBetween(1, 3650).When(x => x.RequiredDays != null).WithMessage("المدة اللازمة بين 1 و3650 يوماً");
            RuleFor(x => x.ProposedManagerUserId).GreaterThan(0).When(x => x.ProposedManagerUserId != null).WithMessage("مدير المشروع المقترح غير صالح");

            RuleFor(x => x.Buildings).Must(b => b.Count <= 2).WithMessage("بيانات المباني سطران على الأكثر (إعادة تأهيل وإنشاء جديد)")
                .Must(b => b.Select(x => x.BuildingType).Distinct().Count() == b.Count).WithMessage("نوع البناء مرة واحدة فقط");
            RuleForEach(x => x.Buildings).ChildRules(b =>
            {
                b.RuleFor(x => x.BuildingType).Must(v => Enum.IsDefined(typeof(StudyBuildingType), v)).WithMessage("نوع البناء غير صالح");
                b.RuleFor(x => x.BuildingCount).GreaterThanOrEqualTo(0).WithMessage("عدد المباني لا يكون سالباً");
                b.RuleFor(x => x.BuildingsArea).GreaterThanOrEqualTo(0).WithMessage("المساحة لا تكون سالبة");
                b.RuleFor(x => x.SiteArea).GreaterThanOrEqualTo(0).When(x => x.SiteArea != null).WithMessage("مساحة الموقع لا تكون سالبة");
                b.RuleFor(x => x.InitialCost).GreaterThanOrEqualTo(0).When(x => x.InitialCost != null).WithMessage("التكلفة لا تكون سالبة");
            });

            RuleFor(x => x.CostItems).Must(c => c.Count <= MaxItems).WithMessage($"بنود التكلفة {MaxItems} على الأكثر");
            RuleForEach(x => x.CostItems).ChildRules(c =>
            {
                c.RuleFor(x => x.Description).NotEmpty().WithMessage("وصف البند مطلوب").MaximumLength(500).WithMessage("وصف البند لا يتجاوز 500 حرف");
                c.RuleFor(x => x.Unit).NotEmpty().WithMessage("وحدة البند مطلوبة").MaximumLength(30).WithMessage("الوحدة لا تتجاوز 30 حرفاً");
                c.RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0).WithMessage("الكمية لا تكون سالبة");
                c.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("سعر الوحدة لا يكون سالباً");
            });

            RuleFor(x => x.StaffEstimates).Must(s => s.Select(x => x.DisciplineId).Distinct().Count() == s.Count)
                .WithMessage("الاختصاص مرة واحدة في الكادر المقترح");
            RuleForEach(x => x.StaffEstimates).ChildRules(s =>
            {
                s.RuleFor(x => x.DisciplineId).GreaterThan(0).WithMessage("اختر الاختصاص");
                s.RuleFor(x => x.StaffCount).InclusiveBetween(0, 1000).WithMessage("العدد بين 0 و1000");
            });

            RuleFor(x => x.LogisticItems).Must(c => c.Count <= MaxItems).WithMessage($"الموارد {MaxItems} على الأكثر");
            RuleForEach(x => x.LogisticItems).SetValidator(new StudyItemFormValidator());
        }
    }

    public class StudyItemFormValidator : AbstractValidator<StudyItemFormDto>
    {
        public StudyItemFormValidator()
        {
            RuleFor(x => x.Item).NotEmpty().WithMessage("اسم البند مطلوب").MaximumLength(200).WithMessage("البند لا يتجاوز 200 حرف");
            RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0).WithMessage("الكمية لا تكون سالبة");
            RuleFor(x => x.Note).MaximumLength(500).WithMessage("الملاحظة لا تتجاوز 500 حرف");
        }
    }

    public class CreateStudyResponseValidator : AbstractValidator<CreateStudyResponseCommand>
    {
        public CreateStudyResponseValidator()
        {
            RuleFor(x => x.Dto).SetValidator(new StudyResponseFormValidator());
            RuleFor(x => x.Dto.AssignmentId).GreaterThan(0).WithMessage("حدد التكليف");
        }
    }

    public class UpdateStudyResponseValidator : AbstractValidator<UpdateStudyResponseCommand>
    {
        public UpdateStudyResponseValidator() => RuleFor(x => x.Dto).SetValidator(new StudyResponseFormValidator());
    }

    public class CancelStudyResponseValidator : AbstractValidator<CancelStudyResponseCommand>
    {
        public CancelStudyResponseValidator() =>
            RuleFor(x => x.Reason).NotEmpty().WithMessage("اذكر سبب الإلغاء").MaximumLength(1000).WithMessage("السبب لا يتجاوز 1000 حرف");
    }

    // ════════════════════ المعالج ════════════════════

    public class StudyResponseHandler :
        IRequestHandler<SearchStudyResponsesQuery, PagedResultDto<StudyResponseDto>>,
        IRequestHandler<GetStudyResponseQuery, StudyResponseDto>,
        IRequestHandler<CreateStudyResponseCommand, StudyResponseDto>,
        IRequestHandler<UpdateStudyResponseCommand, StudyResponseDto>,
        IRequestHandler<DeleteStudyResponseCommand, Unit>,
        IRequestHandler<SubmitStudyResponseCommand, StudyResponseDto>,
        IRequestHandler<CancelStudyResponseCommand, StudyResponseDto>
    {
        private readonly IStudyResponseService _service;
        private readonly IStudyAssignmentService _assignments;
        private readonly IStudyLookupService _lookups;
        private readonly IUserService _users;
        private readonly StudyKit _kit;

        public StudyResponseHandler(IStudyResponseService service, IStudyAssignmentService assignments, IStudyLookupService lookups,
            IUserService users, StudyKit kit)
        {
            _service = service;
            _assignments = assignments;
            _lookups = lookups;
            _users = users;
            _kit = kit;
        }

        private async Task<StudyAssignmentResponse> LoadAsync(int id, Viewer viewer)
        {
            var entity = await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("إشعار الرد غير موجود");
            StudyRules.EnsureInScope(viewer, entity.Assignment.Request.BranchId);
            return entity;
        }

        private StudyResponseDto ToDto(StudyAssignmentResponse e, Viewer viewer)
        {
            var dto = _kit.Mapper.Map<StudyResponseDto>(e);
            var inScope = StudyRules.InScope(viewer, e.Assignment.Request.BranchId);
            dto.CanEdit = inScope && viewer.Has("EditStudyResponse") && StudyRules.IsEditable(e.Status);
            dto.CanSubmit = dto.CanEdit;
            dto.CanDelete = inScope && viewer.Has("DeleteStudyResponse") && e.Status == StudyDocumentStatus.Draft;
            dto.CanCancel = inScope && viewer.Has(StudyPermissions.CancelDocuments)
                            && e.Status is not (StudyDocumentStatus.Draft or StudyDocumentStatus.Cancelled)
                            && (e.StartOrder == null || e.StartOrder.Status == StudyDocumentStatus.Cancelled);
            dto.CanCreateStartOrder = inScope && viewer.Has("CreateStudyStartOrder") && e.Status == StudyDocumentStatus.Approved
                                      && e.Recommendation != StudyRecommendation.NotAvailable && e.StartOrder == null;
            return dto;
        }

        private async Task<StudyResponseDto> DetailAsync(int id, Viewer viewer)
        {
            var entity = await LoadAsync(id, viewer);
            var dto = ToDto(entity, viewer);
            dto.Chain = await _kit.ChainAsync(entity.Assignment.RequestId);
            dto.Approvals = await _kit.ApprovalsAsync(ApprovalOwner.Of(entity));
            return dto;
        }

        private async Task ValidateAsync(StudyResponseFormDto dto, int branchId)
        {
            if (dto.ProposedManagerUserId is int managerId)
                StudyRules.EnsureBranchEmployee(await _users.GetByIdAsync(managerId), branchId, "مدير المشروع المقترح");
            var disciplineIds = dto.StaffEstimates.Select(s => s.DisciplineId).Distinct().ToList();
            if (disciplineIds.Count > 0 && await _lookups.CountActiveDisciplinesAsync(disciplineIds) != disciplineIds.Count)
                throw new KeyNotFoundException("أحد الاختصاصات غير موجود أو غير فعّال");
        }

        /// <summary>الحقول البسيطة + مزامنة الأجزاء: المباني بنوعها والكادر باختصاصه تُحدَّث في مكانها (مفتاحها مركّب)، والبنود تُستبدل</summary>
        private static void Apply(StudyResponseFormDto dto, StudyAssignmentResponse e)
        {
            e.ResponseDate = dto.ResponseDate.Date;
            e.ProposedManagerUserId = dto.ProposedManagerUserId;
            e.Recommendation = (StudyRecommendation)dto.Recommendation;
            e.RequiredDays = dto.RequiredDays;

            var buildingTypes = dto.Buildings.Select(b => (StudyBuildingType)b.BuildingType).ToHashSet();
            e.Buildings.RemoveAll(b => !buildingTypes.Contains(b.BuildingType));
            foreach (var b in dto.Buildings)
            {
                var type = (StudyBuildingType)b.BuildingType;
                var row = e.Buildings.FirstOrDefault(x => x.BuildingType == type);
                if (row == null) e.Buildings.Add(row = new ResponseBuilding { BuildingType = type });
                row.BuildingCount = b.BuildingCount;
                row.BuildingsArea = b.BuildingsArea;
                row.SiteArea = b.SiteArea;
                row.InitialCost = b.InitialCost;
            }

            var disciplines = dto.StaffEstimates.Select(s => s.DisciplineId).ToHashSet();
            e.StaffEstimates.RemoveAll(s => !disciplines.Contains(s.DisciplineId));
            foreach (var s in dto.StaffEstimates)
            {
                var row = e.StaffEstimates.FirstOrDefault(x => x.DisciplineId == s.DisciplineId);
                if (row == null) e.StaffEstimates.Add(row = new ResponseStaffEstimate { DisciplineId = s.DisciplineId });
                row.StaffCount = s.StaffCount;
            }

            e.CostItems.Clear();
            e.CostItems.AddRange(dto.CostItems.Select((c, i) => new ResponseCostItem
            {
                SortOrder = i + 1,
                Description = c.Description.Trim(),
                Unit = c.Unit.Trim(),
                Quantity = c.Quantity,
                UnitPrice = c.UnitPrice
            }));

            e.LogisticItems.Clear();
            e.LogisticItems.AddRange(dto.LogisticItems.Select(x => new ResponseLogisticItem
            {
                Item = x.Item.Trim(),
                Quantity = x.Quantity,
                Note = StudyRequestHandler.Clean(x.Note)
            }));
        }

        public async Task<PagedResultDto<StudyResponseDto>> Handle(SearchStudyResponsesQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var (page, size) = StudyRules.NormalizePaging(request.Filter.Page, request.Filter.PageSize);
            var (items, total) = await _service.SearchAsync(request.Filter, StudyRules.BranchLimit(viewer), page, size);
            return new PagedResultDto<StudyResponseDto>
            {
                Items = items.Select(x => ToDto(x, viewer)).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = size
            };
        }

        public async Task<StudyResponseDto> Handle(GetStudyResponseQuery request, CancellationToken ct) =>
            await DetailAsync(request.Id, await _kit.ViewerAsync());

        public async Task<StudyResponseDto> Handle(CreateStudyResponseCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var assignment = await _assignments.GetByIdAsync(request.Dto.AssignmentId) ?? throw new KeyNotFoundException("التكليف غير موجود");
            StudyRules.EnsureInScope(viewer, assignment.Request.BranchId);
            StudyRules.EnsureApproved(assignment.Status, "التكليف");
            if (await _service.ExistsForAssignmentAsync(assignment.Id))
                throw new InvalidOperationException("للتكليف إشعار رد مسبقاً — الرد لا يُعاد");
            await ValidateAsync(request.Dto, assignment.Request.BranchId);

            var entity = new StudyAssignmentResponse
            {
                AssignmentId = assignment.Id,
                CreatedByUserId = viewer.Id,
                Status = StudyDocumentStatus.Draft,
                CreatedAt = DateTime.Now
            };
            Apply(request.Dto, entity);
            await _service.AddAsync(entity);
            await _kit.LogAsync(assignment.RequestId, StudyEntityType.Response, entity.Id, StudyLogAction.Created,
                $"كتب إشعار الرد على التكليف {assignment.AssignmentNumber}");
            return await DetailAsync(entity.Id, viewer);
        }

        public async Task<StudyResponseDto> Handle(UpdateStudyResponseCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await LoadAsync(request.Id, viewer);
            StudyRules.EnsureEditable(entity.Status);
            await ValidateAsync(request.Dto, entity.Assignment.Request.BranchId);

            Apply(request.Dto, entity);
            entity.Status = StudyDocumentStatus.Draft;
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity, request.Dto.RowVersion);
            await _kit.LogAsync(entity.Assignment.RequestId, StudyEntityType.Response, entity.Id, StudyLogAction.Edited,
                $"عدّل إشعار الرد على {entity.Assignment.AssignmentNumber}");
            return await DetailAsync(entity.Id, viewer);
        }

        public async Task<Unit> Handle(DeleteStudyResponseCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await LoadAsync(request.Id, viewer);
            StudyRules.EnsureDraftForDelete(entity.Status);
            if (entity.StartOrder != null) throw new InvalidOperationException("للرد أمر بدء — لا يُحذف");
            var requestId = entity.Assignment.RequestId;
            var number = entity.Assignment.AssignmentNumber;
            await _service.DeleteAsync(entity);
            await _kit.LogAsync(requestId, StudyEntityType.Response, request.Id, StudyLogAction.Deleted, $"حذف مسودة الرد على {number}");
            return Unit.Value;
        }

        public async Task<StudyResponseDto> Handle(SubmitStudyResponseCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await LoadAsync(request.Id, viewer);
            StudyRules.EnsureEditable(entity.Status);

            entity.Status = StudyDocumentStatus.PendingApproval;
            entity.UpdatedAt = DateTime.Now;
            await _kit.ResetApprovalsAsync(StudyEntityType.Response, entity.Id);
            await _service.UpdateAsync(entity);

            await _kit.LogAsync(entity.Assignment.RequestId, StudyEntityType.Response, entity.Id, StudyLogAction.Submitted,
                $"أرسل إشعار الرد على {entity.Assignment.AssignmentNumber} للاعتماد");
            await _kit.NotifyNextSignersAsync(ApprovalOwner.Of(entity));
            return await DetailAsync(entity.Id, viewer);
        }

        public async Task<StudyResponseDto> Handle(CancelStudyResponseCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await LoadAsync(request.Id, viewer);
            if (entity.Status == StudyDocumentStatus.Cancelled) throw new InvalidOperationException("الرد ملغى مسبقاً");
            if (entity.Status == StudyDocumentStatus.Draft) throw new InvalidOperationException("المسودة تُحذف ولا تُلغى");
            if (entity.StartOrder != null && entity.StartOrder.Status != StudyDocumentStatus.Cancelled)
                throw new InvalidOperationException("للرد أمر بدء قائم — ألغِ أمر البدء أولاً");

            entity.Status = StudyDocumentStatus.Cancelled;
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity);
            await _kit.LogAsync(entity.Assignment.RequestId, StudyEntityType.Response, entity.Id, StudyLogAction.Cancelled,
                $"ألغى إشعار الرد على {entity.Assignment.AssignmentNumber}", request.Reason.Trim());
            return await DetailAsync(entity.Id, viewer);
        }
    }
}
