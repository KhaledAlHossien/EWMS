using FluentValidation;

namespace Application.Features.VacationTypes.Commands.Create
{
    public class CreateVacationTypeCommandValidator
        : AbstractValidator<CreateVacationTypeCommand>
    {
        public CreateVacationTypeCommandValidator()
        {
            RuleFor(x => x.VacationTypeDto.Name)
                .NotEmpty().WithMessage("اسم نوع الإجازة مطلوب")
                .MaximumLength(100).WithMessage("الاسم طويل جداً");

            RuleFor(x => x.VacationTypeDto.Description)
                .MaximumLength(500).WithMessage("الوصف طويل جداً");
        }
    }
}