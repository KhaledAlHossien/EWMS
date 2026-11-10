using MediatR;

namespace Application.Features.CentralStudies.Projects.Commands.Delete
{
    public record DeleteStudyProjectCommand(int Id) : IRequest<Unit>;
}
