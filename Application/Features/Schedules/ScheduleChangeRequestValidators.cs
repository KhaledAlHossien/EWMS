using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.Schedules
{
    // محققات أجسام الطلب المشتركة بين أكثر من أمر
    public class ScheduleExtensionRequestDtoValidator : AbstractValidator<ScheduleExtensionRequestDto>
    {
        public ScheduleExtensionRequestDtoValidator()
        {
            RuleFor(x => x.Mode).Must(m => Enum.IsDefined(typeof(ExtensionMode), m)).WithMessage("نوع التمديد غير صحيح");
            RuleFor(x => x.DayCount).Must(c => Enum.IsDefined(typeof(ExtensionDayCount), c)).WithMessage("طريقة عدّ الأيام غير صحيحة");
            RuleFor(x => x.Reason).MaximumLength(2000).WithMessage("سبب التمديد لا يتجاوز 2000 حرف");
            RuleFor(x => x.Lines).Must(l => l.Count <= ScheduleRules.MaxTasks).WithMessage("عدد بنود التمديد كبير جداً");
            RuleFor(x => x.DocumentVersionIds).Must(d => d.Count <= 20).WithMessage("المستندات الداعمة حتى 20 مستنداً");
        }
    }
}
