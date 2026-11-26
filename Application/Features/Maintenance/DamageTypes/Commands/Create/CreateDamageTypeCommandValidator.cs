using FluentValidation;

namespace Application.Features.Maintenance.DamageTypes.Commands.Create
{
    public class CreateDamageTypeCommandValidator : AbstractValidator<CreateDamageTypeCommand>
    {
        public CreateDamageTypeCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new DamageTypeRequestDtoValidator());
    }
}
