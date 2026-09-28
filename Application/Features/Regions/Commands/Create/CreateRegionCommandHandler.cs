using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;
using RegionEntity = Domain.Entities.Region;

namespace Application.Features.Regions.Commands.Create
{
    public class CreateRegionCommandHandler
        : IRequestHandler<CreateRegionCommand, RegionResponseDto>
    {
        private readonly IRegionService _regionService;
        private readonly IMapper _mapper;

        public CreateRegionCommandHandler(IRegionService regionService, IMapper mapper)
        {
            _regionService = regionService;
            _mapper = mapper;
        }

        public async Task<RegionResponseDto> Handle(
            CreateRegionCommand request,
            CancellationToken cancellationToken)
        {
            if (await _regionService.ExistsByNameAsync(request.RegionDto.Name))
                throw new InvalidOperationException("يوجد منطقة بنفس الاسم مسبقاً");

            var region = _mapper.Map<RegionEntity>(request.RegionDto);
            var created = await _regionService.AddAsync(region);

            return _mapper.Map<RegionResponseDto>(created);
        }
    }
}
