using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.CentralStudiesBranch;
using FluentValidation;
using MediatR;

namespace Application.Features.CentralStudies.Lookups
{
    // قوائم فرع الدراسات (حدّها: كل المؤسسة) — صلاحيات ViewStudyLookups / CreateStudyLookup / EditStudyLookup / DeleteStudyLookup
    public enum StudyLookupKind { AttachmentType = 1, Discipline = 2 }

    public record GetStudyStageTypesQuery(bool ActiveOnly) : IRequest<List<StudyStageTypeDto>>;
    public record CreateStudyStageTypeCommand(StudyStageTypeFormDto Dto) : IRequest<StudyStageTypeDto>;
    public record UpdateStudyStageTypeCommand(int Id, StudyStageTypeFormDto Dto) : IRequest<StudyStageTypeDto>;
    public record DeleteStudyStageTypeCommand(int Id) : IRequest<Unit>;

    public record GetStudyLookupsQuery(StudyLookupKind Kind, bool ActiveOnly) : IRequest<List<StudyLookupDto>>;
    public record CreateStudyLookupCommand(StudyLookupKind Kind, StudyLookupFormDto Dto) : IRequest<StudyLookupDto>;
    public record UpdateStudyLookupCommand(StudyLookupKind Kind, int Id, StudyLookupFormDto Dto) : IRequest<StudyLookupDto>;
    public record DeleteStudyLookupCommand(StudyLookupKind Kind, int Id) : IRequest<Unit>;

    /// <summary>قيم كل تعدادات فرع الدراسات وأسماؤها (لقوائم الواجهة)</summary>
    public record GetStudyEnumsQuery : IRequest<Dictionary<string, List<StudyEnumValueDto>>>;

    // ════════════════════ التحقق ════════════════════

    public class StudyStageTypeFormValidator : AbstractValidator<StudyStageTypeFormDto>
    {
        public StudyStageTypeFormValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("اسم المرحلة مطلوب").MaximumLength(100).WithMessage("اسم المرحلة لا يتجاوز 100 حرف");
            RuleFor(x => x.DefaultOrder).InclusiveBetween(0, 1000).WithMessage("الترتيب بين 0 و1000");
            RuleFor(x => x.DefaultWeight).InclusiveBetween(0, 100).WithMessage("الوزن بين 0 و100");
            RuleFor(x => x.LinkedDocument).Must(v => Enum.IsDefined(typeof(StudyLinkedDocument), v)).WithMessage("المستند المرتبط غير صالح");
        }
    }

    public class CreateStudyStageTypeValidator : AbstractValidator<CreateStudyStageTypeCommand>
    {
        public CreateStudyStageTypeValidator() => RuleFor(x => x.Dto).SetValidator(new StudyStageTypeFormValidator());
    }

    public class UpdateStudyStageTypeValidator : AbstractValidator<UpdateStudyStageTypeCommand>
    {
        public UpdateStudyStageTypeValidator() => RuleFor(x => x.Dto).SetValidator(new StudyStageTypeFormValidator());
    }

    public class StudyLookupFormValidator : AbstractValidator<StudyLookupFormDto>
    {
        public StudyLookupFormValidator() =>
            RuleFor(x => x.Name).NotEmpty().WithMessage("الاسم مطلوب").MaximumLength(100).WithMessage("الاسم لا يتجاوز 100 حرف");
    }

    public class CreateStudyLookupValidator : AbstractValidator<CreateStudyLookupCommand>
    {
        public CreateStudyLookupValidator() => RuleFor(x => x.Dto).SetValidator(new StudyLookupFormValidator());
    }

    public class UpdateStudyLookupValidator : AbstractValidator<UpdateStudyLookupCommand>
    {
        public UpdateStudyLookupValidator() => RuleFor(x => x.Dto).SetValidator(new StudyLookupFormValidator());
    }

    // ════════════════════ المعالج ════════════════════

    public class StudyLookupHandler :
        IRequestHandler<GetStudyStageTypesQuery, List<StudyStageTypeDto>>,
        IRequestHandler<CreateStudyStageTypeCommand, StudyStageTypeDto>,
        IRequestHandler<UpdateStudyStageTypeCommand, StudyStageTypeDto>,
        IRequestHandler<DeleteStudyStageTypeCommand, Unit>,
        IRequestHandler<GetStudyLookupsQuery, List<StudyLookupDto>>,
        IRequestHandler<CreateStudyLookupCommand, StudyLookupDto>,
        IRequestHandler<UpdateStudyLookupCommand, StudyLookupDto>,
        IRequestHandler<DeleteStudyLookupCommand, Unit>,
        IRequestHandler<GetStudyEnumsQuery, Dictionary<string, List<StudyEnumValueDto>>>
    {
        private readonly IStudyLookupService _service;
        private readonly IMapper _mapper;

        public StudyLookupHandler(IStudyLookupService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        // ----- أنواع المراحل -----
        public async Task<List<StudyStageTypeDto>> Handle(GetStudyStageTypesQuery request, CancellationToken ct) =>
            _mapper.Map<List<StudyStageTypeDto>>(await _service.GetStageTypesAsync(request.ActiveOnly));

        public async Task<StudyStageTypeDto> Handle(CreateStudyStageTypeCommand request, CancellationToken ct)
        {
            var entity = _mapper.Map<StudyStageType>(request.Dto);
            entity.Name = entity.Name.Trim();
            if (await _service.StageTypeNameExistsAsync(entity.Name))
                throw new InvalidOperationException("يوجد نوع مرحلة بهذا الاسم مسبقاً");
            await _service.AddAsync(entity);
            return _mapper.Map<StudyStageTypeDto>(entity);
        }

        public async Task<StudyStageTypeDto> Handle(UpdateStudyStageTypeCommand request, CancellationToken ct)
        {
            var entity = await _service.GetStageTypeAsync(request.Id) ?? throw new KeyNotFoundException("نوع المرحلة غير موجود");
            var oldLink = entity.LinkedDocument;
            _mapper.Map(request.Dto, entity);
            entity.Name = entity.Name.Trim();
            // المراحل المرتبطة بمستند أُنشئت مغلقة من ذلك المستند: تغيير الربط بعد الاستخدام يربك المشاريع القائمة
            if (oldLink != entity.LinkedDocument && await _service.StageTypeInUseAsync(entity.Id))
                throw new InvalidOperationException("لا يتغيّر المستند المرتبط بنوع مرحلة مستخدم في مشاريع — أنشئ نوعاً جديداً");
            if (await _service.StageTypeNameExistsAsync(entity.Name, entity.Id))
                throw new InvalidOperationException("يوجد نوع مرحلة آخر بهذا الاسم");
            await _service.SaveAsync();
            return _mapper.Map<StudyStageTypeDto>(entity);
        }

        public async Task<Unit> Handle(DeleteStudyStageTypeCommand request, CancellationToken ct)
        {
            var entity = await _service.GetStageTypeAsync(request.Id) ?? throw new KeyNotFoundException("نوع المرحلة غير موجود");
            if (await _service.StageTypeInUseAsync(entity.Id))
                throw new InvalidOperationException("لا يُحذف نوع مرحلة مستخدم في مشاريع — عطّله بدلاً من ذلك");
            await _service.RemoveAsync(entity);
            return Unit.Value;
        }

        // ----- أنواع المرفقات والاختصاصات -----
        public async Task<List<StudyLookupDto>> Handle(GetStudyLookupsQuery request, CancellationToken ct) =>
            request.Kind == StudyLookupKind.AttachmentType
                ? _mapper.Map<List<StudyLookupDto>>(await _service.GetAttachmentTypesAsync(request.ActiveOnly))
                : _mapper.Map<List<StudyLookupDto>>(await _service.GetDisciplinesAsync(request.ActiveOnly));

        public async Task<StudyLookupDto> Handle(CreateStudyLookupCommand request, CancellationToken ct)
        {
            var name = request.Dto.Name.Trim();
            if (request.Kind == StudyLookupKind.AttachmentType)
            {
                if (await _service.AttachmentTypeNameExistsAsync(name))
                    throw new InvalidOperationException("يوجد نوع مرفق بهذا الاسم مسبقاً");
                var entity = new StudyAttachmentType { Name = name, IsActive = request.Dto.IsActive };
                await _service.AddAsync(entity);
                return _mapper.Map<StudyLookupDto>(entity);
            }
            else
            {
                if (await _service.DisciplineNameExistsAsync(name))
                    throw new InvalidOperationException("يوجد اختصاص بهذا الاسم مسبقاً");
                var entity = new EngineeringDiscipline { Name = name, IsActive = request.Dto.IsActive };
                await _service.AddAsync(entity);
                return _mapper.Map<StudyLookupDto>(entity);
            }
        }

        public async Task<StudyLookupDto> Handle(UpdateStudyLookupCommand request, CancellationToken ct)
        {
            var name = request.Dto.Name.Trim();
            if (request.Kind == StudyLookupKind.AttachmentType)
            {
                var entity = await _service.GetAttachmentTypeAsync(request.Id) ?? throw new KeyNotFoundException("نوع المرفق غير موجود");
                if (await _service.AttachmentTypeNameExistsAsync(name, entity.Id))
                    throw new InvalidOperationException("يوجد نوع مرفق آخر بهذا الاسم");
                entity.Name = name;
                entity.IsActive = request.Dto.IsActive;
                await _service.SaveAsync();
                return _mapper.Map<StudyLookupDto>(entity);
            }
            else
            {
                var entity = await _service.GetDisciplineAsync(request.Id) ?? throw new KeyNotFoundException("الاختصاص غير موجود");
                if (await _service.DisciplineNameExistsAsync(name, entity.Id))
                    throw new InvalidOperationException("يوجد اختصاص آخر بهذا الاسم");
                entity.Name = name;
                entity.IsActive = request.Dto.IsActive;
                await _service.SaveAsync();
                return _mapper.Map<StudyLookupDto>(entity);
            }
        }

        public async Task<Unit> Handle(DeleteStudyLookupCommand request, CancellationToken ct)
        {
            if (request.Kind == StudyLookupKind.AttachmentType)
            {
                var entity = await _service.GetAttachmentTypeAsync(request.Id) ?? throw new KeyNotFoundException("نوع المرفق غير موجود");
                if (await _service.AttachmentTypeInUseAsync(entity.Id))
                    throw new InvalidOperationException("لا يُحذف نوع مرفق مستخدم — عطّله بدلاً من ذلك");
                await _service.RemoveAsync(entity);
            }
            else
            {
                var entity = await _service.GetDisciplineAsync(request.Id) ?? throw new KeyNotFoundException("الاختصاص غير موجود");
                if (await _service.DisciplineInUseAsync(entity.Id))
                    throw new InvalidOperationException("لا يُحذف اختصاص مستخدم في ردود أو أوامر بدء — عطّله بدلاً من ذلك");
                await _service.RemoveAsync(entity);
            }
            return Unit.Value;
        }

        // ----- التعدادات -----
        private static List<StudyEnumValueDto> Values<T>(Func<T, string> name) where T : struct, Enum =>
            Enum.GetValues<T>().Select(v => new StudyEnumValueDto(Convert.ToInt32(v), name(v))).ToList();

        public Task<Dictionary<string, List<StudyEnumValueDto>>> Handle(GetStudyEnumsQuery request, CancellationToken ct) =>
            Task.FromResult(new Dictionary<string, List<StudyEnumValueDto>>
            {
                ["documentStatus"] = Values<StudyDocumentStatus>(StudyRules.Name),
                ["urgency"] = Values<StudyUrgency>(StudyRules.Name),
                ["workType"] = Values<StudyWorkType>(StudyRules.Name),
                ["priority"] = Values<StudyPriority>(StudyRules.Name),
                ["recommendation"] = Values<StudyRecommendation>(StudyRules.Name),
                ["buildingType"] = Values<StudyBuildingType>(StudyRules.Name),
                ["projectStatus"] = Values<StudyProjectStatus>(StudyRules.Name),
                ["stageStatus"] = Values<StudyStageStatus>(StudyRules.Name),
                ["taskStatus"] = Values<StudyTaskStatus>(StudyRules.Name),
                ["taskTarget"] = Values<StudyTaskTarget>(StudyRules.Name),
                ["drawingKind"] = Values<DrawingKind>(StudyRules.Name),
                ["drawingStatus"] = Values<DrawingStatus>(StudyRules.Name),
                ["decision"] = Values<StudyDecision>(StudyRules.Name),
                ["signerRole"] = Values<StudySignerRole>(StudyRules.Name),
                ["linkedDocument"] = Values<StudyLinkedDocument>(StudyRules.Name),
                ["entityType"] = Values<StudyEntityType>(StudyRules.Name),
                ["logAction"] = Values<StudyLogAction>(StudyRules.Name),
            });
    }
}
