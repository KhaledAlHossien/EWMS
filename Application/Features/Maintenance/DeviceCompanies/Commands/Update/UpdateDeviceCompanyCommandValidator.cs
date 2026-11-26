using FluentValidation;

namespace Application.Features.Maintenance.DeviceCompanies.Commands.Update
{
    public class UpdateDeviceCompanyCommandValidator : AbstractValidator<UpdateDeviceCompanyCommand>
    {
        public UpdateDeviceCompanyCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new DeviceCompanyRequestDtoValidator());
    }
}
