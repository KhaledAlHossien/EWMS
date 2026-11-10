using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Commands.AddMember
{
    public record AddStudyProjectMemberCommand(int ProjectDepartmentId, ProjectMemberFormDto Dto) : IRequest<StudyProjectDto>;
}
