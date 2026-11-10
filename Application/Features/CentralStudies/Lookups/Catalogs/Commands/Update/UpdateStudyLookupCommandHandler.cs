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
