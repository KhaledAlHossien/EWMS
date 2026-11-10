using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.DesignApprovals.Queries.GetById
{
    public record GetDesignApprovalQuery(int Id) : IRequest<DesignApprovalDto>;
}
