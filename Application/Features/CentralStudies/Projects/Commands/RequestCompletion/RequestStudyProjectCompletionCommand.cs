using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Commands.RequestCompletion
{
    public record RequestStudyProjectCompletionCommand(int Id) : IRequest<StudyProjectDto>;
}
