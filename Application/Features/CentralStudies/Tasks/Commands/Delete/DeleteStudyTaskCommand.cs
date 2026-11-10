using MediatR;

namespace Application.Features.CentralStudies.Tasks.Commands.Delete
{
    public record DeleteStudyTaskCommand(int Id) : IRequest<Unit>;
}
