using FluentValidation;

namespace Application.Features.CentralStudies.ArchitecturalVersions.Commands.Update
{
    public class UpdateArchitecturalVersionCommandValidator : AbstractValidator<UpdateArchitecturalVersionCommand>
    {
        public UpdateArchitecturalVersionCommandValidator() => RuleFor(x => x.Dto).SetValidator(new ArchitecturalVersionFormValidator());
    }
}
