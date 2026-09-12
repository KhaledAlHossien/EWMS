using FluentValidation;

namespace Application.Features.Projects.Commands.Update
{
    public class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
    {
        public UpdateProjectCommandValidator()
        {
            RuleFor(x => x.ProjectDto.Name)
                .NotEmpty().WithMessage("اسم المشروع مطلوب")
                .MaximumLength(200).WithMessage("اسم المشروع طويل جداً");
        }
    }
}
