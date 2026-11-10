using MediatR;

namespace Application.Features.CentralStudies.DesignApprovals.Commands.Delete
{
    public record DeleteDesignApprovalCommand(int Id) : IRequest<Unit>;
}
