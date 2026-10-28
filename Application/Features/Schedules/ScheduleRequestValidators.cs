using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.Schedules
{
    // محققات أجسام الطلب المشتركة بين أكثر من أمر
    public class ScheduleTaskRequestDtoValidator : AbstractValidator<ScheduleTaskRequestDto>
    {
        public ScheduleTaskRequestDtoValidator()
        {
            RuleFor(x => x.Code).MaximumLength(30).WithMessage("رمز البند لا يتجاوز 30 حرفاً");
            RuleFor(x => x.Note).MaximumLength(2000).WithMessage("ملاحظة البند لا تتجاوز 2000 حرف");
        }
    }
}
