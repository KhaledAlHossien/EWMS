using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.CentralStudies.Requests;
using Application.Features.CentralStudies.Responses;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using FluentValidation;
using MediatR;

namespace Application.Features.CentralStudies.StartOrders
{
    // أمر بدء دراسة مشروع (PIO) — واحد لكل رد معتمد (التوصية ليست «غير متاح»)، يرتبط بالرد فقط.
    // يوقّعه رئيس القسم المختص ثم رئيس الفرع. المهندسون مستخدمون محددون (فريق الدراسة).
    public record SearchStudyStartOrdersQuery(StudyDocumentFilterDto Filter) : IRequest<PagedResultDto<StudyStartOrderDto>>;
    public record GetStudyStartOrderQuery(int Id) : IRequest<StudyStartOrderDto>;
    public record CreateStudyStartOrderCommand(StudyStartOrderFormDto Dto) : IRequest<StudyStartOrderDto>;
    public record UpdateStudyStartOrderCommand(int Id, StudyStartOrderFormDto Dto) : IRequest<StudyStartOrderDto>;
    public record DeleteStudyStartOrderCommand(int Id) : IRequest<Unit>;
    public record SubmitStudyStartOrderCommand(int Id) : IRequest<StudyStartOrderDto>;
    public record CancelStudyStartOrderCommand(int Id, string Reason) : IRequest<StudyStartOrderDto>;

    // ════════════════════ التحقق ════════════════════

    public class StudyStartOrderFormValidator : AbstractValidator<StudyStartOrderFormDto>
    {
        public StudyStartOrderFormValidator()
        {
            RuleFor(x => x.CompetentDepartmentId).GreaterThan(0).WithMessage("اختر القسم المختص");
            RuleFor(x => x.ProjectManagerUserId).GreaterThan(0).WithMessage("اختر مدير المشروع");
            RuleFor(x => x.OrderDate).NotEmpty().WithMessage("تاريخ الأمر مطلوب");
            RuleFor(x => x.StudyStartDate).NotEmpty().WithMessage("تاريخ بدء الدراسة مطلوب");
            RuleFor(x => x.StudyDeadline).NotEmpty().WithMessage("الحد الأقصى للإنهاء مطلوب")
                .GreaterThanOrEqualTo(x => x.StudyStartDate).WithMessage("الحد الأقصى للإنهاء لا يسبق تاريخ البدء");
            RuleFor(x => x.Engineers).Must(e => e.Count <= 100).WithMessage("المهندسون 100 على الأكثر")
                .Must(e => e.Select(x => x.EngineerUserId).Distinct().Count() == e.Count).WithMessage("المهندس مرة واحدة في الأمر");
            RuleForEach(x => x.Engineers).ChildRules(e =>
            {
                e.RuleFor(x => x.EngineerUserId).GreaterThan(0).WithMessage("اختر المهندس");
                e.RuleFor(x => x.DisciplineId).GreaterThan(0).WithMessage("اختر اختصاص المهندس");
            });
            RuleFor(x => x.Resources).Must(c => c.Count <= StudyResponseFormValidator.MaxItems).WithMessage("الموارد 200 على الأكثر");
            RuleForEach(x => x.Resources).SetValidator(new StudyItemFormValidator());
        }
    }

    public class CreateStudyStartOrderValidator : AbstractValidator<CreateStudyStartOrderCommand>
    {
        public CreateStudyStartOrderValidator()
        {
            RuleFor(x => x.Dto).SetValidator(new StudyStartOrderFormValidator());
            RuleFor(x => x.Dto.ResponseId).GreaterThan(0).WithMessage("حدد إشعار الرد");
        }
    }

    public class UpdateStudyStartOrderValidator : AbstractValidator<UpdateStudyStartOrderCommand>
    {
        public UpdateStudyStartOrderValidator() => RuleFor(x => x.Dto).SetValidator(new StudyStartOrderFormValidator());
    }

    public class CancelStudyStartOrderValidator : AbstractValidator<CancelStudyStartOrderCommand>
    {
        public CancelStudyStartOrderValidator() =>
            RuleFor(x => x.Reason).NotEmpty().WithMessage("اذكر سبب الإلغاء").MaximumLength(1000).WithMessage("السبب لا يتجاوز 1000 حرف");
    }

    // ════════════════════ المعالج ════════════════════

    public class StudyStartOrderHandler :
        IRequestHandler<SearchStudyStartOrdersQuery, PagedResultDto<StudyStartOrderDto>>,
        IRequestHandler<GetStudyStartOrderQuery, StudyStartOrderDto>,
        IRequestHandler<CreateStudyStartOrderCommand, StudyStartOrderDto>,
        IRequestHandler<UpdateStudyStartOrderCommand, StudyStartOrderDto>,
        IRequestHandler<DeleteStudyStartOrderCommand, Unit>,
        IRequestHandler<SubmitStudyStartOrderCommand, StudyStartOrderDto>,
        IRequestHandler<CancelStudyStartOrderCommand, StudyStartOrderDto>
    {
        private readonly IStudyStartOrderService _service;
        private readonly IStudyResponseService _responses;
        private readonly IStudyNumberService _numbers;
        private readonly IStudyLookupService _lookups;
        private readonly IDepartmentService _departments;
        private readonly IUserService _users;
        private readonly StudyKit _kit;

        public StudyStartOrderHandler(IStudyStartOrderService service, IStudyResponseService responses, IStudyNumberService numbers,
            IStudyLookupService lookups, IDepartmentService departments, IUserService users, StudyKit kit)
        {
            _service = service;
            _responses = responses;
            _numbers = numbers;
            _lookups = lookups;
            _departments = departments;
            _users = users;
            _kit = kit;
        }

        private async Task<StudyStartOrder> LoadAsync(int id, Viewer viewer)
        {
            var entity = await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("أمر البدء غير موجود");
            StudyRules.EnsureInScope(viewer, entity.Response.Assignment.Request.BranchId);
            return entity;
        }

        private StudyStartOrderDto ToDto(StudyStartOrder e, Viewer viewer)
        {
            var dto = _kit.Mapper.Map<StudyStartOrderDto>(e);
            var inScope = StudyRules.InScope(viewer, e.Response.Assignment.Request.BranchId);
            dto.CanEdit = inScope && viewer.Has("EditStudyStartOrder") && StudyRules.IsEditable(e.Status);
            dto.CanSubmit = dto.CanEdit;
            dto.CanDelete = inScope && viewer.Has("DeleteStudyStartOrder") && e.Status == StudyDocumentStatus.Draft;
            dto.CanCancel = inScope && viewer.Has(StudyPermissions.CancelDocuments)
                            && e.Status is not (StudyDocumentStatus.Draft or StudyDocumentStatus.Cancelled)
                            && (e.Project == null || e.Project.Status == StudyProjectStatus.Cancelled);
            dto.CanCreateProject = inScope && viewer.Has("CreateStudyProject") && e.Status == StudyDocumentStatus.Approved && e.Project == null;
            return dto;
        }

        private async Task<StudyStartOrderDto> DetailAsync(int id, Viewer viewer)
        {
            var entity = await LoadAsync(id, viewer);
            var dto = ToDto(entity, viewer);
            dto.Chain = await _kit.ChainAsync(entity.Response.Assignment.RequestId);
            dto.Approvals = await _kit.ApprovalsAsync(ApprovalOwner.Of(entity));
            await _kit.AddWhoIsNextAsync(dto, StudyEntityType.StartOrder, entity.Id, ApprovalOwner.Of(entity));
            return dto;
        }

        private async Task ValidateAsync(StudyStartOrderFormDto dto, int branchId)
        {
            var department = await _departments.GetByIdAsync(dto.CompetentDepartmentId) ?? throw new KeyNotFoundException("القسم المختص غير موجود");
            if (department.BranchId != branchId)
                throw new InvalidOperationException("القسم المختص يجب أن يكون من أقسام فرع الطلب");

            StudyRules.EnsureBranchEmployee(await _users.GetByIdAsync(dto.ProjectManagerUserId), branchId, "مدير المشروع");

            foreach (var engineerId in dto.Engineers.Select(e => e.EngineerUserId).Distinct())
                StudyRules.EnsureBranchEmployee(await _users.GetByIdAsync(engineerId), branchId, "أحد المهندسين");
            var disciplineIds = dto.Engineers.Select(e => e.DisciplineId).Distinct().ToList();
            if (disciplineIds.Count > 0 && await _lookups.CountActiveDisciplinesAsync(disciplineIds) != disciplineIds.Count)
                throw new KeyNotFoundException("أحد الاختصاصات غير موجود أو غير فعّال");
        }

        /// <summary>الحقول + مزامنة المهندسين بمعرّفهم (مفتاح مركّب) واستبدال الموارد</summary>
        private static void Apply(StudyStartOrderFormDto dto, StudyStartOrder e)
        {
            e.CompetentDepartmentId = dto.CompetentDepartmentId;
            e.ProjectManagerUserId = dto.ProjectManagerUserId;
            e.OrderDate = dto.OrderDate.Date;
            e.StudyStartDate = dto.StudyStartDate.Date;
            e.StudyDeadline = dto.StudyDeadline.Date;

            var engineerIds = dto.Engineers.Select(x => x.EngineerUserId).ToHashSet();
            e.Engineers.RemoveAll(x => !engineerIds.Contains(x.EngineerUserId));
            foreach (var en in dto.Engineers)
            {
                var row = e.Engineers.FirstOrDefault(x => x.EngineerUserId == en.EngineerUserId);
                if (row == null) e.Engineers.Add(row = new StartOrderEngineer { EngineerUserId = en.EngineerUserId });
                row.DisciplineId = en.DisciplineId;
            }

            e.Resources.Clear();
            e.Resources.AddRange(dto.Resources.Select(x => new StartOrderResource
            {
                Item = x.Item.Trim(),
                Quantity = x.Quantity,
                Note = StudyRequestHandler.Clean(x.Note)
            }));
        }

        public async Task<PagedResultDto<StudyStartOrderDto>> Handle(SearchStudyStartOrdersQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var (page, size) = StudyRules.NormalizePaging(request.Filter.Page, request.Filter.PageSize);
            var (items, total) = await _service.SearchAsync(request.Filter, StudyRules.BranchLimit(viewer), page, size);
            return new PagedResultDto<StudyStartOrderDto>
            {
                Items = items.Select(x => ToDto(x, viewer)).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = size
            };
        }

        public async Task<StudyStartOrderDto> Handle(GetStudyStartOrderQuery request, CancellationToken ct) =>
            await DetailAsync(request.Id, await _kit.ViewerAsync());

        public async Task<StudyStartOrderDto> Handle(CreateStudyStartOrderCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var response = await _responses.GetByIdAsync(request.Dto.ResponseId) ?? throw new KeyNotFoundException("إشعار الرد غير موجود");
            var branchId = response.Assignment.Request.BranchId;
            StudyRules.EnsureInScope(viewer, branchId);
            StudyRules.EnsureApproved(response.Status, "إشعار الرد");
            if (response.Recommendation == StudyRecommendation.NotAvailable)
                throw new InvalidOperationException("التوصية الفنية «غير متاح» — لا يصدر أمر بدء لهذه الدراسة");
            if (await _service.ExistsForResponseAsync(response.Id))
                throw new InvalidOperationException("للرد أمر بدء مسبقاً — أمر واحد لكل رد");
            await ValidateAsync(request.Dto, branchId);

            var entity = new StudyStartOrder
            {
                ResponseId = response.Id,
                CreatedByUserId = viewer.Id,
                Status = StudyDocumentStatus.Draft,
                CreatedAt = DateTime.Now
            };
            Apply(request.Dto, entity);
            var year = DateTime.Today.Year;
            entity.OrderNumber = StudyRules.FormatNumber(StudyDocumentType.StartOrder, year,
                await _numbers.NextAsync(StudyDocumentType.StartOrder, year));

            await _service.AddAsync(entity);
            await _kit.LogAsync(response.Assignment.RequestId, StudyEntityType.StartOrder, entity.Id, StudyLogAction.Created,
                $"أصدر أمر البدء {entity.OrderNumber}");
            return await DetailAsync(entity.Id, viewer);
        }

        public async Task<StudyStartOrderDto> Handle(UpdateStudyStartOrderCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await LoadAsync(request.Id, viewer);
            StudyRules.EnsureEditable(entity.Status);
            await ValidateAsync(request.Dto, entity.Response.Assignment.Request.BranchId);

            Apply(request.Dto, entity);
            entity.Status = StudyDocumentStatus.Draft;
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity, request.Dto.RowVersion);
            await _kit.LogAsync(entity.Response.Assignment.RequestId, StudyEntityType.StartOrder, entity.Id, StudyLogAction.Edited,
                $"عدّل أمر البدء {entity.OrderNumber}");
            return await DetailAsync(entity.Id, viewer);
        }

        public async Task<Unit> Handle(DeleteStudyStartOrderCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await LoadAsync(request.Id, viewer);
            StudyRules.EnsureDraftForDelete(entity.Status);
            if (entity.Project != null) throw new InvalidOperationException("لأمر البدء مشروع — لا يُحذف");
            var requestId = entity.Response.Assignment.RequestId;
            var number = entity.OrderNumber;
            await _service.DeleteAsync(entity);
            await _kit.LogAsync(requestId, StudyEntityType.StartOrder, request.Id, StudyLogAction.Deleted, $"حذف مسودة أمر البدء {number}");
            return Unit.Value;
        }

        public async Task<StudyStartOrderDto> Handle(SubmitStudyStartOrderCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await LoadAsync(request.Id, viewer);
            StudyRules.EnsureEditable(entity.Status);
            if (entity.Engineers.Count == 0)
                throw new InvalidOperationException("خصّص مهندساً واحداً على الأقل للدراسة قبل الإرسال");

            entity.Status = StudyDocumentStatus.PendingApproval;
            entity.UpdatedAt = DateTime.Now;
            await _kit.ResetApprovalsAsync(StudyEntityType.StartOrder, entity.Id);
            await _service.UpdateAsync(entity);

            await _kit.LogAsync(entity.Response.Assignment.RequestId, StudyEntityType.StartOrder, entity.Id, StudyLogAction.Submitted,
                $"أرسل أمر البدء {entity.OrderNumber} للاعتماد");
            await _kit.NotifyNextSignersAsync(ApprovalOwner.Of(entity));
            return await DetailAsync(entity.Id, viewer);
        }

        public async Task<StudyStartOrderDto> Handle(CancelStudyStartOrderCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await LoadAsync(request.Id, viewer);
            if (entity.Status == StudyDocumentStatus.Cancelled) throw new InvalidOperationException("أمر البدء ملغى مسبقاً");
            if (entity.Status == StudyDocumentStatus.Draft) throw new InvalidOperationException("المسودة تُحذف ولا تُلغى");
            if (entity.Project != null && entity.Project.Status != StudyProjectStatus.Cancelled)
                throw new InvalidOperationException("لأمر البدء مشروع قائم — ألغِ المشروع أولاً");

            entity.Status = StudyDocumentStatus.Cancelled;
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity);
            await _kit.LogAsync(entity.Response.Assignment.RequestId, StudyEntityType.StartOrder, entity.Id, StudyLogAction.Cancelled,
                $"ألغى أمر البدء {entity.OrderNumber}", request.Reason.Trim());
            return await DetailAsync(entity.Id, viewer);
        }
    }
}
