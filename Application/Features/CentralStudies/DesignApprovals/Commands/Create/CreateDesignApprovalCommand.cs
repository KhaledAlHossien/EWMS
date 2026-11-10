using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.DesignApprovals.Commands.Create
{
    public record CreateDesignApprovalCommand(DesignApprovalFormDto Dto) : IRequest<DesignApprovalDto>;
}
