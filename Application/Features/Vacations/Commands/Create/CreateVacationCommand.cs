using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.Vacations.Commands.Create
{
    public class CreateVacationCommand : IRequest<VacationResponseDto>
    {
        public CreateVacationRequestDto VacationDto { get; set; }
        public int UserId { get; set; }

        public CreateVacationCommand(CreateVacationRequestDto dto, int userId)
        {
            VacationDto = dto;
            UserId = userId;
        }
    }
}
