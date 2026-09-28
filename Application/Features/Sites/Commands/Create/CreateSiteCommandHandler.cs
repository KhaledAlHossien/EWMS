using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;
using SiteEntity = Domain.Entities.Site;

namespace Application.Features.Sites.Commands.Create
{
    public class CreateSiteCommandHandler
        : IRequestHandler<CreateSiteCommand, SiteResponseDto>
    {
        private readonly ISiteService _siteService;
        private readonly IRegionService _regionService;
        private readonly IMapper _mapper;

        public CreateSiteCommandHandler(
            ISiteService siteService,
            IRegionService regionService,
            IMapper mapper)
        {
            _siteService = siteService;
            _regionService = regionService;
            _mapper = mapper;
        }

        public async Task<SiteResponseDto> Handle(
            CreateSiteCommand request,
            CancellationToken cancellationToken)
        {
            if (!await _regionService.ExistsAsync(request.SiteDto.RegionId))
                throw new KeyNotFoundException("المنطقة المحددة غير موجودة");

            if (await _siteService.ExistsByNameAsync(request.SiteDto.Name))
                throw new InvalidOperationException("يوجد موقع بنفس الاسم مسبقاً");

            var site = _mapper.Map<SiteEntity>(request.SiteDto);
            var created = await _siteService.AddAsync(site);

            var withDetails = await _siteService.GetByIdAsync(created.Id) ?? created;
            return _mapper.Map<SiteResponseDto>(withDetails);
        }
    }
}
