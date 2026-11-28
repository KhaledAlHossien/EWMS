using MediatR;

namespace Application.Features.CentralStudies.Lookups.StudyBranches.Commands.Delete
{
    public record DeleteStudyBranchCommand(int Id) : IRequest<Unit>;
}
