using FluentValidation;

namespace Application.Features.Projects.Commands.Create
{
    public class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
    {
        public CreateProjectCommandValidator()
        {
            RuleFor(x => x.ProjectDto.Name)
                .NotEmpty().WithMessage("اسم المشروع مطلوب")
                .MaximumLength(200).WithMessage("اسم المشروع طويل جداً");

            RuleFor(x => x.ProjectDto.CurrentDepartmentId)
                .GreaterThan(0).WithMessage("القسم مطلوب");
        }
    }
}
