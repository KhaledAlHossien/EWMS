using Application.DTOs.Response;
using MediatR;

namespace Application.Features.DeviceInventory.Sites.Queries.GetNearby
{
    /// <summary>مواقع قريبة من نقطة (تنبيه قبل إضافة موقع مكرر في السجل المشترك)</summary>
    public record GetNearbySitesQuery(double Latitude, double Longitude, int? ExcludeId) : IRequest<List<NearbySiteDto>>;
}
