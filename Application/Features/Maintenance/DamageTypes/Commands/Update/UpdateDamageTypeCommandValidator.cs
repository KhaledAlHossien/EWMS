using FluentValidation;

namespace Application.Features.Maintenance.DamageTypes.Commands.Update
{
    public class UpdateDamageTypeCommandValidator : AbstractValidator<UpdateDamageTypeCommand>
    {
        public UpdateDamageTypeCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new DamageTypeRequestDtoValidator());
    }
}
