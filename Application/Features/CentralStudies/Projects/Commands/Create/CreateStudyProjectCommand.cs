using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Commands.Create
{
    public record CreateStudyProjectCommand(StudyProjectFormDto Dto) : IRequest<StudyProjectDto>;
}
