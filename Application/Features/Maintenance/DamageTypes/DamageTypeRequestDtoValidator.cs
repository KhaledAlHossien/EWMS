using Application.DTOs.Request;
using FluentValidation;

namespace Application.Features.Maintenance.DamageTypes
{
    public class DamageTypeRequestDtoValidator : AbstractValidator<DamageTypeRequestDto>
    {
        public DamageTypeRequestDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم نوع العطل مطلوب")
                .MaximumLength(100).WithMessage("اسم نوع العطل لا يتجاوز 100 حرف");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("الوصف لا يتجاوز 500 حرف");
        }
    }
}
