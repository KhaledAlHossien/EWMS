using FluentValidation;

namespace Application.Features.Offices.Commands.Create
{
    public class CreateOfficeCommandValidator : AbstractValidator<CreateOfficeCommand>
    {
        public CreateOfficeCommandValidator()
        {
            RuleFor(x => x.OfficeDto.Name)
                .NotEmpty().WithMessage("اسم المكتب مطلوب")
                .MaximumLength(100).WithMessage("اسم المكتب لا يتجاوز 100 حرف");

            RuleFor(x => x.OfficeDto.Description)
                .MaximumLength(500).WithMessage("الوصف لا يتجاوز 500 حرف");

            RuleFor(x => x.OfficeDto.DepartmentId)
                .GreaterThan(0).WithMessage("يجب اختيار قسم");
        }
    }
}
