using FluentValidation;

namespace Application.Features.DeviceInventory.Installations.Commands.Update
{
    public class UpdateInstallationCommandValidator : AbstractValidator<UpdateInstallationCommand>
    {
        public UpdateInstallationCommandValidator() => RuleFor(x => x.Dto).SetValidator(new ExistingInstallationValidator());
    }
}
