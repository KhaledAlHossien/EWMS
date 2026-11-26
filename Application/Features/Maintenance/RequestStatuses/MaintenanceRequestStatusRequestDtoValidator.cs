using Application.DTOs.Request;
using Domain.Entities.Maintenance;
using FluentValidation;

namespace Application.Features.Maintenance.RequestStatuses
{
    public class MaintenanceRequestStatusRequestDtoValidator : AbstractValidator<MaintenanceRequestStatusRequestDto>
    {
        public MaintenanceRequestStatusRequestDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم الحالة مطلوب")
                .MaximumLength(100).WithMessage("اسم الحالة لا يتجاوز 100 حرف");

            RuleFor(x => x.Color)
                .NotEmpty().WithMessage("لون الحالة مطلوب")
                .Matches("^#[0-9A-Fa-f]{6}$").WithMessage("اللون يجب أن يكون بصيغة #RRGGBB");

            RuleFor(x => x.Stage)
                .Must(s => Enum.IsDefined(typeof(MaintenanceStage), s)).WithMessage("اختر مرحلة الحالة");
        }
    }
}
