using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Projects.Queries.GetByDepartment
{
    public record GetProjectsByDepartmentQuery(int DepartmentId) : IRequest<List<ProjectResponseDto>>;
}
