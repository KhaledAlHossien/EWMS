using FluentValidation;

namespace Application.Features.CentralStudies.Lookups.Catalogs.Commands.Create
{
    public class CreateStudyLookupCommandValidator : AbstractValidator<CreateStudyLookupCommand>
    {
        public CreateStudyLookupCommandValidator() => RuleFor(x => x.Dto)
            .SetValidator(x => new StudyLookupFormValidator(x.Kind == StudyLookupKind.Beneficiary ? 200 : 100,
                requireCode: x.Kind == StudyLookupKind.Beneficiary));
    }
}
