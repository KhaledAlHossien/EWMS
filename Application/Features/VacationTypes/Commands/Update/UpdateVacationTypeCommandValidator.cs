using FluentValidation;

namespace Application.Features.VacationTypes.Commands.Update
{
    public class UpdateVacationTypeCommandValidator
        : AbstractValidator<UpdateVacationTypeCommand>
    {
        public UpdateVacationTypeCommandValidator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0).WithMessage("رقم نوع الإجازة غير صحيح");

            RuleFor(x => x.VacationTypeDto.Name)
                .NotEmpty().WithMessage("اسم نوع الإجازة مطلوب")
                .MaximumLength(100).WithMessage("الاسم طويل جداً");

            RuleFor(x => x.VacationTypeDto.Description)
                .MaximumLength(500).WithMessage("الوصف طويل جداً");
        }
    }
}