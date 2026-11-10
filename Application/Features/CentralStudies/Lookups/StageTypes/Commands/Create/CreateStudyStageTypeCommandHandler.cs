using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.StageTypes.Commands.Create
{
    public class CreateStudyStageTypeCommandHandler : IRequestHandler<CreateStudyStageTypeCommand, StudyStageTypeDto>
    {
        private readonly IStudyLookupService _service;
        private readonly IMapper _mapper;

        public CreateStudyStageTypeCommandHandler(IStudyLookupService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<StudyStageTypeDto> Handle(CreateStudyStageTypeCommand request, CancellationToken ct)
        {
            var entity = _mapper.Map<StudyStageType>(request.Dto);
            entity.Name = entity.Name.Trim();
            if (await _service.StageTypeNameExistsAsync(entity.Name))
                throw new InvalidOperationException("يوجد نوع مرحلة بهذا الاسم مسبقاً");
            await _service.AddAsync(entity);
            return _mapper.Map<StudyStageTypeDto>(entity);
        }
    }
}
