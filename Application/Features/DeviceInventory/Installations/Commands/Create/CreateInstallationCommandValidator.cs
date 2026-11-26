using FluentValidation;

namespace Application.Features.DeviceInventory.Installations.Commands.Create
{
    public class CreateInstallationCommandValidator : AbstractValidator<CreateInstallationCommand>
    {
        public CreateInstallationCommandValidator() => RuleFor(x => x.Dto).SetValidator(new NewInstallationValidator());
    }
}
