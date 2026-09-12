using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Department.Queries.GetById
{
    public record GetDepartmentByIdQuery(int Id) : IRequest<DepartmentResponseDto>;
}