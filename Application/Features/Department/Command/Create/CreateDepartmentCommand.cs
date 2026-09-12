using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.Department.Command.Create
{
    public record CreateDepartmentCommand(CreateDepartmentRequestDto DepartmentDto)
        : IRequest<DepartmentResponseDto>;
}
