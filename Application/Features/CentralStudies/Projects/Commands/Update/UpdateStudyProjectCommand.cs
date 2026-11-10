using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Commands.Update
{
    public record UpdateStudyProjectCommand(int Id, StudyProjectFormDto Dto) : IRequest<StudyProjectDto>;
}
