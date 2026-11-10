using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Commands.Cancel
{
    public record CancelStudyProjectCommand(int Id, string Reason) : IRequest<StudyProjectDto>;
}
