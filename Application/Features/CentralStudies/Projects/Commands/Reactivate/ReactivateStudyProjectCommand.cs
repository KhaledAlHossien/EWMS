using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Commands.Reactivate
{
    public record ReactivateStudyProjectCommand(int Id) : IRequest<StudyProjectDto>;
}
