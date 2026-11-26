using FluentValidation;

namespace Application.Features.Maintenance.DeviceCompanies.Commands.Create
{
    public class CreateDeviceCompanyCommandValidator : AbstractValidator<CreateDeviceCompanyCommand>
    {
        public CreateDeviceCompanyCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new DeviceCompanyRequestDtoValidator());
    }
}
