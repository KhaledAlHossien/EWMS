using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Commands.RemoveMember
{
    public record RemoveStudyProjectMemberCommand(int ProjectDepartmentId, int UserId) : IRequest<StudyProjectDto>;
}
