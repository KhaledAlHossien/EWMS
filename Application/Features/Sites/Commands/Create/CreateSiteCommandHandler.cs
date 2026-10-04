using Application.DTOs.Response;
using Application.Features.Sites;
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
        private readonly IMapper _mapper;

        public CreateSiteCommandHandler(
            ISiteService siteService,
            IMapper mapper)
        {
            _siteService = siteService;
            _mapper = mapper;
        }

        public async Task<SiteResponseDto> Handle(
            CreateSiteCommand request,
            CancellationToken cancellationToken)
        {

            if (await _siteService.ExistsByNameAsync(request.SiteDto.Name))
                throw new InvalidOperationException("يوجد موقع بنفس الاسم مسبقاً");

            var site = _mapper.Map<SiteEntity>(request.SiteDto);
            site.GovernorateCode = GovernorateResolver.CodeFor(request.SiteDto.Latitude, request.SiteDto.Longitude);
            var created = await _siteService.AddAsync(site);

            var withDetails = await _siteService.GetByIdAsync(created.Id) ?? created;
            return _mapper.Map<SiteResponseDto>(withDetails);
        }
    }
}
