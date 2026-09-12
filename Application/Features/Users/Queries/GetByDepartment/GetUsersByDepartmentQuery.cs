using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Users.Queries.GetByDepartment
{
    public record GetUsersByDepartmentQuery(int DepartmentId) : IRequest<List<UserResponseDto>>;
}
