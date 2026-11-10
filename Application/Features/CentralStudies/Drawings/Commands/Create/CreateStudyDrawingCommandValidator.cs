using Domain.Entities.CentralStudiesBranch;
using FluentValidation;

namespace Application.Features.CentralStudies.Drawings.Commands.Create
{
    public class CreateStudyDrawingCommandValidator : AbstractValidator<CreateStudyDrawingCommand>
    {
        public CreateStudyDrawingCommandValidator()
        {
            RuleFor(x => x.Dto).SetValidator(new StudyDrawingFormValidator());
            RuleFor(x => x.Dto.ProjectId).GreaterThan(0).WithMessage("حدد المشروع");
            RuleFor(x => x.Dto.Kind).Must(v => Enum.IsDefined(typeof(DrawingKind), v)).WithMessage("حدد نوع المخطط");
        }
    }
}
