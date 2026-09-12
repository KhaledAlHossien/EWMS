using MediatR;

namespace Application.Features.Projects.Commands.Delete
{
    public record DeleteProjectCommand(int Id) : IRequest<bool>;
}
