using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.Catalogs.Commands.Update
{
    public class UpdateStudyLookupCommandHandler : IRequestHandler<UpdateStudyLookupCommand, StudyLookupDto>
    {
        private readonly IStudyLookupService _service;
        private readonly IMapper _mapper;

        public UpdateStudyLookupCommandHandler(IStudyLookupService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<StudyLookupDto> Handle(UpdateStudyLookupCommand request, CancellationToken ct)
        {
            var name = request.Dto.Name.Trim();
            if (request.Kind == StudyLookupKind.Beneficiary)
            {
                var entity = await _service.GetBeneficiaryAsync(request.Id) ?? throw new KeyNotFoundException("الجهة المستفيدة غير موجودة");
                if (await _service.BeneficiaryNameExistsAsync(name, entity.Id))
                    throw new InvalidOperationException("توجد جهة مستفيدة أخرى بهذا الاسم");
                var code = StudyRules.Clean(request.Dto.Code) ?? throw new InvalidOperationException("رمز الجهة مطلوب");
                if (await _service.BeneficiaryCodeExistsAsync(code, entity.Id))
                    throw new InvalidOperationException("رمز الجهة مستخدم لجهة مستفيدة أخرى");
                entity.Name = name;
                entity.Code = code;
                entity.IsActive = request.Dto.IsActive;
                await _service.SaveAsync();
                return _mapper.Map<StudyLookupDto>(entity);
            }
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
    }
}
