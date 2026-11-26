using Application.DTOs.Response;
using MediatR;

namespace Application.Features.MapLayers.Entries.Queries.GetSiteEntries
{
    public record GetSiteEntriesQuery(int SiteId) : IRequest<SiteEntriesDto>;
}
