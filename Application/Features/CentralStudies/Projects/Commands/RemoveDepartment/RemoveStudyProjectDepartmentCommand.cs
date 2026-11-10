using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Commands.RemoveDepartment
{
    public record RemoveStudyProjectDepartmentCommand(int ProjectDepartmentId) : IRequest<StudyProjectDto>;
}
