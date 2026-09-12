using FluentValidation;

namespace Application.Features.Roles.Commands.Update
{
    public class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
    {
        public UpdateRoleCommandValidator()
        {
            RuleFor(x => x.RoleDto.Name)
                .NotEmpty().WithMessage("اسم الدور مطلوب")
                .MaximumLength(100).WithMessage("اسم الدور طويل جداً");
        }
    }
}
