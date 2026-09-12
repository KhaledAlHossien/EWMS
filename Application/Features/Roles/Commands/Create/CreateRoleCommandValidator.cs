using FluentValidation;

namespace Application.Features.Roles.Commands.Create
{
    public class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
    {
        public CreateRoleCommandValidator()
        {
            RuleFor(x => x.RoleDto.Name)
                .NotEmpty().WithMessage("اسم الدور مطلوب")
                .MaximumLength(100).WithMessage("اسم الدور طويل جداً");
        }
    }
}
