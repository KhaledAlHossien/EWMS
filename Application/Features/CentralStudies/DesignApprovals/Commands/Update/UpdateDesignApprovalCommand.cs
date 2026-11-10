using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.DesignApprovals.Commands.Update
{
    public record UpdateDesignApprovalCommand(int Id, DesignApprovalFormDto Dto) : IRequest<DesignApprovalDto>;
}
