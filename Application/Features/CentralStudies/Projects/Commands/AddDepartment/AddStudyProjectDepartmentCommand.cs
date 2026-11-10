using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Commands.AddDepartment
{
    public record AddStudyProjectDepartmentCommand(int ProjectId, int DepartmentId) : IRequest<StudyProjectDto>;
}
