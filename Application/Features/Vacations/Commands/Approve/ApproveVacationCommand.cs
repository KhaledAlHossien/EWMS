using Application.DTOs.Request;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.Vacations.Commands.Approve
{
    public class ApproveVacationCommand : IRequest<Unit>
    {
        public int VacationId { get; set; }
        public ApproveVacationRequestDto Dto { get; set; }

        public ApproveVacationCommand(int vacationId, ApproveVacationRequestDto dto)
        {
            VacationId = vacationId;
            Dto = dto;
        }
    }
}
