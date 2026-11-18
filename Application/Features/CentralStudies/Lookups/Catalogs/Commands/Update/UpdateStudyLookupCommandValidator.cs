using FluentValidation;

namespace Application.Features.CentralStudies.Lookups.Catalogs.Commands.Update
{
    public class UpdateStudyLookupCommandValidator : AbstractValidator<UpdateStudyLookupCommand>
    {
        public UpdateStudyLookupCommandValidator() => RuleFor(x => x.Dto)
            .SetValidator(x => new StudyLookupFormValidator(x.Kind == StudyLookupKind.Beneficiary ? 200 : 100,
                requireCode: x.Kind == StudyLookupKind.Beneficiary));
    }
}
