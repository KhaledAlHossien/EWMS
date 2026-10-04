using Application.DTOs.Response;
using Application.Features.Sites;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Sites.Commands.Update
{
    public class UpdateSiteCommandHandler
        : IRequestHandler<UpdateSiteCommand, SiteResponseDto>
    {
        private readonly ISiteService _siteService;
        private readonly IMapper _mapper;

        public UpdateSiteCommandHandler(
            ISiteService siteService,
            IMapper mapper)
        {
            _siteService = siteService;
            _mapper = mapper;
        }

        public async Task<SiteResponseDto> Handle(
            UpdateSiteCommand request,
            CancellationToken cancellationToken)
        {
            var site = await _siteService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("الموقع غير موجود");


            if (await _siteService.ExistsByNameAsync(request.SiteDto.Name, request.Id))
                throw new InvalidOperationException("يوجد موقع آخر بنفس الاسم");

            _mapper.Map(request.SiteDto, site);
            site.GovernorateCode = GovernorateResolver.CodeFor(request.SiteDto.Latitude, request.SiteDto.Longitude);
            await _siteService.UpdateAsync(site);

            var withDetails = await _siteService.GetByIdAsync(site.Id) ?? site;
            return _mapper.Map<SiteResponseDto>(withDetails);
        }
    }
}
