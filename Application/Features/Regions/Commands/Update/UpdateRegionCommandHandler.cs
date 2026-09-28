using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Regions.Commands.Update
{
    public class UpdateRegionCommandHandler
        : IRequestHandler<UpdateRegionCommand, RegionResponseDto>
    {
        private readonly IRegionService _regionService;
        private readonly IMapper _mapper;

        public UpdateRegionCommandHandler(IRegionService regionService, IMapper mapper)
        {
            _regionService = regionService;
            _mapper = mapper;
        }

        public async Task<RegionResponseDto> Handle(
            UpdateRegionCommand request,
            CancellationToken cancellationToken)
        {
            var region = await _regionService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("المنطقة غير موجودة");

            if (await _regionService.ExistsByNameAsync(request.RegionDto.Name, request.Id))
                throw new InvalidOperationException("يوجد منطقة أخرى بنفس الاسم");

            _mapper.Map(request.RegionDto, region);
            await _regionService.UpdateAsync(region);

            return _mapper.Map<RegionResponseDto>(region);
        }
    }
}
