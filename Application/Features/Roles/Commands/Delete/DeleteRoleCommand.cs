using MediatR;

namespace Application.Features.Roles.Commands.Delete
{
    public record DeleteRoleCommand(int Id) : IRequest<bool>;
}
