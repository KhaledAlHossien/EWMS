using MediatR;

namespace Application.Features.Branches.Commands.Delete
{
    public record DeleteBranchCommand(int Id) : IRequest<bool>;
}
