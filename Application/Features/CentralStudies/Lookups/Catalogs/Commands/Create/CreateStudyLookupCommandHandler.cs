using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.Catalogs.Commands.Create
{
    public class CreateStudyLookupCommandHandler : IRequestHandler<CreateStudyLookupCommand, StudyLookupDto>
    {
        private readonly IStudyLookupService _service;
        private readonly IMapper _mapper;

        public CreateStudyLookupCommandHandler(IStudyLookupService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

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
    }
}
