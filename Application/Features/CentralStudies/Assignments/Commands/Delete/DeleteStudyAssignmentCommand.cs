using MediatR;

namespace Application.Features.CentralStudies.Assignments.Commands.Delete
{
    public record DeleteStudyAssignmentCommand(int Id) : IRequest<Unit>;
}
