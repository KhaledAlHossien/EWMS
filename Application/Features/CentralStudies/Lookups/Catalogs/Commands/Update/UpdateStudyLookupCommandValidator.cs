using FluentValidation;

namespace Application.Features.CentralStudies.Lookups.Catalogs.Commands.Update
{
    public class UpdateStudyLookupCommandValidator : AbstractValidator<UpdateStudyLookupCommand>
    {
        public UpdateStudyLookupCommandValidator() => RuleFor(x => x.Dto).SetValidator(new StudyLookupFormValidator());
    }
}
