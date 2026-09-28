using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Sites.Queries.GetById
{
    public class GetSiteByIdQueryHandler
        : IRequestHandler<GetSiteByIdQuery, SiteResponseDto>
    {
        private readonly ISiteService _siteService;
        private readonly IMapper _mapper;

        public GetSiteByIdQueryHandler(ISiteService siteService, IMapper mapper)
        {
            _siteService = siteService;
            _mapper = mapper;
        }

        public async Task<SiteResponseDto> Handle(
            GetSiteByIdQuery request,
            CancellationToken cancellationToken)
        {
            var site = await _siteService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("الموقع غير موجود");

            return _mapper.Map<SiteResponseDto>(site);
        }
    }
}
