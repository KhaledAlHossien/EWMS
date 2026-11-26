using FluentValidation;

namespace Application.Features.Maintenance.Tasks.Commands.Update
{
    public class UpdateMaintenanceTaskCommandValidator : AbstractValidator<UpdateMaintenanceTaskCommand>
    {
        public UpdateMaintenanceTaskCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new SaveMaintenanceTaskDtoValidator());
    }
}
