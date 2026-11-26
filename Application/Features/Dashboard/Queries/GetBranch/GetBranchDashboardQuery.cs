using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Dashboard.Queries.GetBranch
{
    public record GetBranchDashboardQuery(int? BranchId) : IRequest<BranchDashboardDto>;
}
