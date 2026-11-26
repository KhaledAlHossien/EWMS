using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.Requests.Queries.GetStats
{
    /// <summary>إحصائيات الصيانة ضمن نطاقي (طلبات + مهام)</summary>
    public record GetMaintenanceStatsQuery : IRequest<MaintenanceStatsDto>;
}
