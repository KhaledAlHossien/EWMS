using FluentValidation;

namespace Application.Features.Maintenance.Tasks.Commands.Create
{
    public class CreateMaintenanceTaskCommandValidator : AbstractValidator<CreateMaintenanceTaskCommand>
    {
        public CreateMaintenanceTaskCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new SaveMaintenanceTaskDtoValidator());
    }
}
