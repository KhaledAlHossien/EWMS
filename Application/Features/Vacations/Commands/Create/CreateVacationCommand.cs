using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.Vacations.Commands.Create
{
    // قد ينتج عن الطلب الواحد أكثر من إجازة (مثلاً: أيام مدفوعة + أيام غير مدفوعة
    // عند تجاوز الحد الشهري)، لذلك النتيجة قائمة وليست عنصراً واحداً
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
