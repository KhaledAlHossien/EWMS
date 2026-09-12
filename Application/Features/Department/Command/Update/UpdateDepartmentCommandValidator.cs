using FluentValidation;

namespace Application.Features.Department.Command.Update
{
    public class UpdateDepartmentCommandValidator : AbstractValidator<UpdateDepartmentCommand>
    {
        public UpdateDepartmentCommandValidator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0).WithMessage("معرف القسم غير صالح");

            RuleFor(x => x.DepartmentDto.Name)
                .NotEmpty().WithMessage("اسم القسم مطلوب")
                .MaximumLength(100).WithMessage("اسم القسم لا يتجاوز 100 حرف");

            RuleFor(x => x.DepartmentDto.Description)
                .MaximumLength(500).WithMessage("الوصف لا يتجاوز 500 حرف");

            RuleFor(x => x.DepartmentDto.BranchId)
                .GreaterThan(0).WithMessage("يجب اختيار فرع");
        }
    }
}