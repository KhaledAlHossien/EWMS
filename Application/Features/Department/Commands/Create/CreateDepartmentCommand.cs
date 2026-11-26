using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Department.Commands.Create
{
    public record CreateDepartmentCommand(CreateDepartmentRequestDto DepartmentDto)
        : IRequest<DepartmentResponseDto>;
}
