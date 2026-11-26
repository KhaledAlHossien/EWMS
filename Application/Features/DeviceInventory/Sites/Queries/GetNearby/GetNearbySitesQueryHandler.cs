using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.DeviceInventory.Sites.Queries.GetNearby
{
    public class GetNearbySitesQueryHandler : IRequestHandler<GetNearbySitesQuery, List<NearbySiteDto>>
    {
        private readonly ISiteService _sites;

        public GetNearbySitesQueryHandler(ISiteService sites)
        {
            _sites = sites;
        }

        public async Task<List<NearbySiteDto>> Handle(GetNearbySitesQuery request, CancellationToken ct) =>
            (await _sites.GetNearbyAsync(request.Latitude, request.Longitude, NearbyRadiusMeters, request.ExcludeId))
                .Select(n => new NearbySiteDto { Id = n.Id, Name = n.Name, DistanceMeters = n.DistanceMeters })
                .ToList();

        /// <summary>ما دون هذه المسافة يُعدّ «موقعاً قريباً» يُنبَّه إليه قبل الإضافة</summary>
        public const double NearbyRadiusMeters = 200;
    }
}
