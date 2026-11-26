using Application.DTOs.Response;
using MediatR;

namespace Application.Features.DeviceInventory.Sites.Queries.GetById
{
    public class GetSiteByIdQueryHandler : IRequestHandler<GetSiteByIdQuery, SiteResponseDto>
    {
        private readonly SiteSupport _support;

        public GetSiteByIdQueryHandler(SiteSupport support)
        {
            _support = support;
        }

        public Task<SiteResponseDto> Handle(GetSiteByIdQuery request, CancellationToken ct) => _support.DtoAsync(request.Id);
    }
}
