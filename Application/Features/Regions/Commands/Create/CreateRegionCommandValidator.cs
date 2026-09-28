using FluentValidation;

namespace Application.Features.Regions.Commands.Create
{
    public class CreateRegionCommandValidator : AbstractValidator<CreateRegionCommand>
    {
        public CreateRegionCommandValidator()
        {
            RuleFor(x => x.RegionDto.Name)
                .NotEmpty().WithMessage("اسم المنطقة مطلوب")
                .MaximumLength(100).WithMessage("اسم المنطقة لا يتجاوز 100 حرف");

            RuleFor(x => x.RegionDto.Description)
                .MaximumLength(500).WithMessage("الوصف لا يتجاوز 500 حرف");
        }
    }
}
