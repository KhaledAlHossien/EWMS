using FluentValidation;

namespace Application.Features.CentralStudies.ArchitecturalVersions.Commands.Create
{
    public class CreateArchitecturalVersionCommandValidator : AbstractValidator<CreateArchitecturalVersionCommand>
    {
        public CreateArchitecturalVersionCommandValidator() => RuleFor(x => x.Dto).SetValidator(new ArchitecturalVersionFormValidator());
    }
}
