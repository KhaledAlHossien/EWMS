using FluentValidation;

namespace Application.Features.Maintenance.Requests.Commands.Update
{
    public class UpdateMaintenanceRequestCommandValidator : AbstractValidator<UpdateMaintenanceRequestCommand>
    {
        public UpdateMaintenanceRequestCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new SaveMaintenanceRequestDtoValidator());
    }
}
