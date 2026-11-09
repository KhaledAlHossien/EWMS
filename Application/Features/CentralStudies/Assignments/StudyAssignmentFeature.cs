using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.CentralStudies.Requests;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using FluentValidation;
using MediatR;

namespace Application.Features.CentralStudies.Assignments
{
    // التكليف الداخلي لبيان مشروع (IPA) — واحد لكل طلب معتمد، يعتمده رئيس الفرع. حدّ الصلاحيات: تكليفات طلبات فرعه.
    public record SearchStudyAssignmentsQuery(StudyDocumentFilterDto Filter) : IRequest<PagedResultDto<StudyAssignmentDto>>;
    public record GetStudyAssignmentQuery(int Id) : IRequest<StudyAssignmentDto>;
    public record CreateStudyAssignmentCommand(StudyAssignmentFormDto Dto) : IRequest<StudyAssignmentDto>;
    public record UpdateStudyAssignmentCommand(int Id, StudyAssignmentFormDto Dto) : IRequest<StudyAssignmentDto>;
    public record DeleteStudyAssignmentCommand(int Id) : IRequest<Unit>;
    public record SubmitStudyAssignmentCommand(int Id) : IRequest<StudyAssignmentDto>;
    public record CancelStudyAssignmentCommand(int Id, string Reason) : IRequest<StudyAssignmentDto>;

    // ════════════════════ التحقق ════════════════════

    public class StudyAssignmentFormValidator : AbstractValidator<StudyAssignmentFormDto>
    {
        public StudyAssignmentFormValidator()
        {
            RuleFor(x => x.FollowingDepartmentId).GreaterThan(0).WithMessage("اختر القسم المتابع");
            RuleFor(x => x.AssignmentDate).NotEmpty().WithMessage("تاريخ التكليف مطلوب");
            RuleFor(x => x.ProjectCode).MaximumLength(50).WithMessage("كود المشروع لا يتجاوز 50 حرفاً");
            RuleFor(x => x.AllocatedBudget).GreaterThanOrEqualTo(0).When(x => x.AllocatedBudget != null).WithMessage("الميزانية لا تكون سالبة");
            RuleFor(x => x.ScopeSummary).NotEmpty().WithMessage("ملخص نطاق العمل والمتطلبات مطلوب")
                .MaximumLength(4000).WithMessage("ملخص النطاق لا يتجاوز 4000 حرف");
            RuleFor(x => x.ReferralDate).NotEmpty().WithMessage("تاريخ الإحالة الداخلي مطلوب");
            RuleFor(x => x.ResponseDeadline).NotEmpty().WithMessage("الحد الأقصى للرد مطلوب")
                .GreaterThanOrEqualTo(x => x.ReferralDate).WithMessage("الحد الأقصى للرد لا يسبق تاريخ الإحالة");
            RuleFor(x => x.Priority).Must(v => Enum.IsDefined(typeof(StudyPriority), v)).WithMessage("حدد درجة الأهمية");
        }
    }

    public class CreateStudyAssignmentValidator : AbstractValidator<CreateStudyAssignmentCommand>
    {
        public CreateStudyAssignmentValidator()
        {
            RuleFor(x => x.Dto).SetValidator(new StudyAssignmentFormValidator());
            RuleFor(x => x.Dto.RequestId).GreaterThan(0).WithMessage("حدد طلب تجهيز الدراسة");
        }
    }

    public class UpdateStudyAssignmentValidator : AbstractValidator<UpdateStudyAssignmentCommand>
    {
        public UpdateStudyAssignmentValidator() => RuleFor(x => x.Dto).SetValidator(new StudyAssignmentFormValidator());
    }

    public class CancelStudyAssignmentValidator : AbstractValidator<CancelStudyAssignmentCommand>
    {
        public CancelStudyAssignmentValidator() =>
            RuleFor(x => x.Reason).NotEmpty().WithMessage("اذكر سبب الإلغاء").MaximumLength(1000).WithMessage("السبب لا يتجاوز 1000 حرف");
    }

    // ════════════════════ المعالج ════════════════════

    public class StudyAssignmentHandler :
        IRequestHandler<SearchStudyAssignmentsQuery, PagedResultDto<StudyAssignmentDto>>,
        IRequestHandler<GetStudyAssignmentQuery, StudyAssignmentDto>,
        IRequestHandler<CreateStudyAssignmentCommand, StudyAssignmentDto>,
        IRequestHandler<UpdateStudyAssignmentCommand, StudyAssignmentDto>,
        IRequestHandler<DeleteStudyAssignmentCommand, Unit>,
        IRequestHandler<SubmitStudyAssignmentCommand, StudyAssignmentDto>,
        IRequestHandler<CancelStudyAssignmentCommand, StudyAssignmentDto>
    {
        private readonly IStudyAssignmentService _service;
        private readonly IStudyRequestService _requests;
        private readonly IStudyNumberService _numbers;
        private readonly IDepartmentService _departments;
        private readonly StudyKit _kit;

        public StudyAssignmentHandler(IStudyAssignmentService service, IStudyRequestService requests, IStudyNumberService numbers,
            IDepartmentService departments, StudyKit kit)
        {
            _service = service;
            _requests = requests;
            _numbers = numbers;
            _departments = departments;
            _kit = kit;
        }

        private async Task<StudyAssignment> LoadAsync(int id, Viewer viewer)
        {
            var entity = await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("التكليف غير موجود");
            StudyRules.EnsureInScope(viewer, entity.Request.BranchId);
            return entity;
        }

        private StudyAssignmentDto ToDto(StudyAssignment e, Viewer viewer)
        {
            var dto = _kit.Mapper.Map<StudyAssignmentDto>(e);
            var inScope = StudyRules.InScope(viewer, e.Request.BranchId);
            dto.CanEdit = inScope && viewer.Has("EditStudyAssignment") && StudyRules.IsEditable(e.Status);
            dto.CanSubmit = dto.CanEdit;
            dto.CanDelete = inScope && viewer.Has("DeleteStudyAssignment") && e.Status == StudyDocumentStatus.Draft;
            dto.CanCancel = inScope && viewer.Has(StudyPermissions.CancelDocuments)
                            && e.Status is not (StudyDocumentStatus.Draft or StudyDocumentStatus.Cancelled)
                            && (e.Response == null || e.Response.Status == StudyDocumentStatus.Cancelled);
            dto.CanCreateResponse = inScope && viewer.Has("CreateStudyResponse")
                                    && e.Status == StudyDocumentStatus.Approved && e.Response == null;
            return dto;
        }

        private async Task<StudyAssignmentDto> DetailAsync(int id, Viewer viewer)
        {
            var entity = await LoadAsync(id, viewer);
            var dto = ToDto(entity, viewer);
            dto.Chain = await _kit.ChainAsync(entity.RequestId);
            dto.Approvals = await _kit.ApprovalsAsync(ApprovalOwner.Of(entity));
            await _kit.AddWhoIsNextAsync(dto, StudyEntityType.Assignment, entity.Id, ApprovalOwner.Of(entity));
            return dto;
        }

        /// <summary>القسم المتابع من أقسام فرع الطلب، وكود المشروع فريد</summary>
        private async Task ValidateAsync(StudyAssignmentFormDto dto, int branchId, int? excludeId)
        {
            var department = await _departments.GetByIdAsync(dto.FollowingDepartmentId)
                ?? throw new KeyNotFoundException("القسم المتابع غير موجود");
            if (department.BranchId != branchId)
                throw new InvalidOperationException("القسم المتابع يجب أن يكون من أقسام فرع الطلب");
            var code = StudyRequestHandler.Clean(dto.ProjectCode);
            if (code != null && await _service.ProjectCodeExistsAsync(code, excludeId))
                throw new InvalidOperationException("كود المشروع مستخدم في تكليف أو مشروع آخر");
        }

        private static void Apply(StudyAssignmentFormDto dto, StudyAssignment e)
        {
            e.FollowingDepartmentId = dto.FollowingDepartmentId;
            e.AssignmentDate = dto.AssignmentDate.Date;
            e.ProjectCode = StudyRequestHandler.Clean(dto.ProjectCode);
            e.AllocatedBudget = dto.AllocatedBudget;
            e.ScopeSummary = dto.ScopeSummary.Trim();
            e.NeedsTechnicalEvaluation = dto.NeedsTechnicalEvaluation;
            e.NeedsResourceEstimate = dto.NeedsResourceEstimate;
            e.NeedsPreliminaryBudget = dto.NeedsPreliminaryBudget;
            e.NeedsFinalRecommendation = dto.NeedsFinalRecommendation;
            e.ReferralDate = dto.ReferralDate.Date;
            e.ResponseDeadline = dto.ResponseDeadline.Date;
            e.Priority = (StudyPriority)dto.Priority;
        }

        public async Task<PagedResultDto<StudyAssignmentDto>> Handle(SearchStudyAssignmentsQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var (page, size) = StudyRules.NormalizePaging(request.Filter.Page, request.Filter.PageSize);
            var (items, total) = await _service.SearchAsync(request.Filter, StudyRules.BranchLimit(viewer), page, size);
            return new PagedResultDto<StudyAssignmentDto>
            {
                Items = items.Select(x => ToDto(x, viewer)).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = size
            };
        }

        public async Task<StudyAssignmentDto> Handle(GetStudyAssignmentQuery request, CancellationToken ct) =>
            await DetailAsync(request.Id, await _kit.ViewerAsync());

        public async Task<StudyAssignmentDto> Handle(CreateStudyAssignmentCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var studyRequest = await _requests.GetByIdAsync(request.Dto.RequestId) ?? throw new KeyNotFoundException("الطلب غير موجود");
            StudyRules.EnsureInScope(viewer, studyRequest.BranchId);
            StudyRules.EnsureApproved(studyRequest.Status, "الطلب");
            if (await _service.ExistsForRequestAsync(studyRequest.Id))
                throw new InvalidOperationException("للطلب تكليف داخلي مسبقاً — تكليف واحد لكل طلب");
            await ValidateAsync(request.Dto, studyRequest.BranchId, null);

            var entity = new StudyAssignment
            {
                RequestId = studyRequest.Id,
                CreatedByUserId = viewer.Id,
                Status = StudyDocumentStatus.Draft,
                CreatedAt = DateTime.Now
            };
            Apply(request.Dto, entity);
            var year = DateTime.Today.Year;
            entity.AssignmentNumber = StudyRules.FormatNumber(StudyDocumentType.Assignment, year,
                await _numbers.NextAsync(StudyDocumentType.Assignment, year));

            await _service.AddAsync(entity);
            await _kit.LogAsync(entity.RequestId, StudyEntityType.Assignment, entity.Id, StudyLogAction.Created,
                $"أصدر التكليف الداخلي {entity.AssignmentNumber}");
            return await DetailAsync(entity.Id, viewer);
        }

        public async Task<StudyAssignmentDto> Handle(UpdateStudyAssignmentCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await LoadAsync(request.Id, viewer);
            StudyRules.EnsureEditable(entity.Status);
            await ValidateAsync(request.Dto, entity.Request.BranchId, entity.Id);

            Apply(request.Dto, entity);
            entity.Status = StudyDocumentStatus.Draft;
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity, request.Dto.RowVersion);
            await _kit.LogAsync(entity.RequestId, StudyEntityType.Assignment, entity.Id, StudyLogAction.Edited,
                $"عدّل التكليف {entity.AssignmentNumber}");
            return await DetailAsync(entity.Id, viewer);
        }

        public async Task<Unit> Handle(DeleteStudyAssignmentCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await LoadAsync(request.Id, viewer);
            StudyRules.EnsureDraftForDelete(entity.Status);
            if (entity.Response != null) throw new InvalidOperationException("للتكليف إشعار رد — لا يُحذف");
            var requestId = entity.RequestId;
            var number = entity.AssignmentNumber;
            await _service.DeleteAsync(entity);
            await _kit.LogAsync(requestId, StudyEntityType.Assignment, request.Id, StudyLogAction.Deleted, $"حذف مسودة التكليف {number}");
            return Unit.Value;
        }

        public async Task<StudyAssignmentDto> Handle(SubmitStudyAssignmentCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await LoadAsync(request.Id, viewer);
            StudyRules.EnsureEditable(entity.Status);

            entity.Status = StudyDocumentStatus.PendingApproval;
            entity.UpdatedAt = DateTime.Now;
            await _kit.ResetApprovalsAsync(StudyEntityType.Assignment, entity.Id);
            await _service.UpdateAsync(entity);   // يحفظ سطور التوقيع مع المستند

            await _kit.LogAsync(entity.RequestId, StudyEntityType.Assignment, entity.Id, StudyLogAction.Submitted,
                $"أرسل التكليف {entity.AssignmentNumber} للاعتماد");
            await _kit.NotifyNextSignersAsync(ApprovalOwner.Of(entity));
            return await DetailAsync(entity.Id, viewer);
        }

        public async Task<StudyAssignmentDto> Handle(CancelStudyAssignmentCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await LoadAsync(request.Id, viewer);
            if (entity.Status == StudyDocumentStatus.Cancelled) throw new InvalidOperationException("التكليف ملغى مسبقاً");
            if (entity.Status == StudyDocumentStatus.Draft) throw new InvalidOperationException("المسودة تُحذف ولا تُلغى");
            if (entity.Response != null && entity.Response.Status != StudyDocumentStatus.Cancelled)
                throw new InvalidOperationException("للتكليف إشعار رد قائم — ألغِ الرد أولاً");

            entity.Status = StudyDocumentStatus.Cancelled;
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity);
            await _kit.LogAsync(entity.RequestId, StudyEntityType.Assignment, entity.Id, StudyLogAction.Cancelled,
                $"ألغى التكليف {entity.AssignmentNumber}", request.Reason.Trim());
            return await DetailAsync(entity.Id, viewer);
        }
    }
}
