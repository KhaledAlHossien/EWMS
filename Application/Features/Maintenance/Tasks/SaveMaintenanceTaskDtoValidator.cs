using Application.DTOs.Request;
using FluentValidation;

namespace Application.Features.Maintenance.Tasks
{
    public class SaveMaintenanceTaskDtoValidator : AbstractValidator<SaveMaintenanceTaskDto>
    {
        public SaveMaintenanceTaskDtoValidator()
        {
            RuleFor(x => x.TaskLocation)
                .NotEmpty().WithMessage("مكان المهمة مطلوب")
                .MaximumLength(200).WithMessage("مكان المهمة لا يتجاوز 200 حرف");

            RuleFor(x => x.RequestingParty)
                .NotEmpty().WithMessage("الجهة الطالبة مطلوبة")
                .MaximumLength(200).WithMessage("الجهة الطالبة لا تتجاوز 200 حرف");

            RuleFor(x => x.RequiredWork)
                .NotEmpty().WithMessage("الأعمال المطلوبة مطلوبة")
                .MaximumLength(2000).WithMessage("الأعمال المطلوبة لا تتجاوز 2000 حرف");

            RuleFor(x => x.CompletedWorks)
                .MaximumLength(2000).WithMessage("الأعمال المنجزة لا تتجاوز 2000 حرف");

            RuleFor(x => x.CompletedAt)
                .GreaterThanOrEqualTo(x => x.StartedAt!.Value)
                .When(x => x.StartedAt != null && x.CompletedAt != null)
                .WithMessage("تاريخ الإنجاز لا يمكن أن يسبق تاريخ البدء");
        }
    }
}
