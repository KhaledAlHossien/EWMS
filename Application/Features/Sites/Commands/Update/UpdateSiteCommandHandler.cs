using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Sites.Commands.Update
{
    public class UpdateSiteCommandHandler
        : IRequestHandler<UpdateSiteCommand, SiteResponseDto>
    {
        private readonly ISiteService _siteService;
        private readonly IRegionService _regionService;
        private readonly IMapper _mapper;

        public UpdateSiteCommandHandler(
            ISiteService siteService,
            IRegionService regionService,
            IMapper mapper)
        {
            _siteService = siteService;
            _regionService = regionService;
            _mapper = mapper;
        }

        public async Task<SiteResponseDto> Handle(
            UpdateSiteCommand request,
            CancellationToken cancellationToken)
        {
            var site = await _siteService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("الموقع غير موجود");

            if (!await _regionService.ExistsAsync(request.SiteDto.RegionId))
                throw new KeyNotFoundException("المنطقة المحددة غير موجودة");

            if (await _siteService.ExistsByNameAsync(request.SiteDto.Name, request.Id))
                throw new InvalidOperationException("يوجد موقع آخر بنفس الاسم");

            _mapper.Map(request.SiteDto, site);
            await _siteService.UpdateAsync(site);

            var withDetails = await _siteService.GetByIdAsync(site.Id) ?? site;
            return _mapper.Map<SiteResponseDto>(withDetails);
        }
    }
}
