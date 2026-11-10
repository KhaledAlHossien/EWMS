using FluentValidation;

namespace Application.Features.CentralStudies.Stages.Commands.Create
{
    public class CreateStudyStageCommandValidator : AbstractValidator<CreateStudyStageCommand>
    {
        public CreateStudyStageCommandValidator()
        {
            RuleFor(x => x.Dto).SetValidator(new StudyStageFormValidator());
            RuleFor(x => x.Dto.ProjectId).GreaterThan(0).WithMessage("حدد المشروع");
            RuleFor(x => x.Dto.StageTypeId).GreaterThan(0).WithMessage("اختر نوع المرحلة");
        }
    }
}
