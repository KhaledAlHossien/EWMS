using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Dashboard.Queries.GetMy
{
    public record GetMyDashboardQuery : IRequest<EmployeeDashboardDto>;
}
