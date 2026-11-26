using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.Requests.Queries.GetMy
{
    /// <summary>طلبات الصيانة التي أنا عميلها</summary>
    public record GetMyMaintenanceRequestsQuery : IRequest<List<MyMaintenanceRequestDto>>;
}
