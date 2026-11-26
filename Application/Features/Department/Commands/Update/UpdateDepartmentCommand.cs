using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Department.Commands.Update
{
    public record UpdateDepartmentCommand(int Id, UpdateDepartmentRequestDto DepartmentDto)
        : IRequest<DepartmentResponseDto>;
}
