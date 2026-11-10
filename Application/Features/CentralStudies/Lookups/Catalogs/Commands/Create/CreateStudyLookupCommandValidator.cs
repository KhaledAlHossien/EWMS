using FluentValidation;

namespace Application.Features.CentralStudies.Lookups.Catalogs.Commands.Create
{
    public class CreateStudyLookupCommandValidator : AbstractValidator<CreateStudyLookupCommand>
    {
        public CreateStudyLookupCommandValidator() => RuleFor(x => x.Dto).SetValidator(new StudyLookupFormValidator());
    }
}
