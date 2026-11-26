using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.Requests.Queries.GetActivities
{
    /// <summary>سجل الطلب (الأحدث أولاً)</summary>
    public record GetMaintenanceRequestActivitiesQuery(int Id) : IRequest<List<MaintenanceActivityDto>>;
}
