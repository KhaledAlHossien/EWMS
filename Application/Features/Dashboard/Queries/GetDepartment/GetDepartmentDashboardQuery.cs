using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Dashboard.Queries.GetDepartment
{
    public record GetDepartmentDashboardQuery(int? DepartmentId) : IRequest<DepartmentDashboardDto>;
}
