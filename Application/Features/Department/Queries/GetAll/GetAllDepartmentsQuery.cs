using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Department.Queries.GetAll
{
    public record GetAllDepartmentsQuery : IRequest<List<DepartmentResponseDto>>;
}