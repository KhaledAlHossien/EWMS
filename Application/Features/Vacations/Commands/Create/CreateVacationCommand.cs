using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.Vacations.Commands.Create
{
    // النتيجة قائمة بعنصر واحد: بقي شكلها كما كان (كان الطلب يُقسَّم مدفوع/غير مدفوع عند التقديم قبل 2026-10-04)
    public class CreateVacationCommand : IRequest<List<VacationResponseDto>>
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
