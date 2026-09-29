using Application.Common;
using FluentValidation;

namespace Application.Features.Sites.Commands.Update
{
    public class UpdateSiteCommandValidator : AbstractValidator<UpdateSiteCommand>
    {
        public UpdateSiteCommandValidator()
        {
            RuleFor(x => x.SiteDto.Name)
                .NotEmpty().WithMessage("اسم الموقع مطلوب")
                .MaximumLength(100).WithMessage("اسم الموقع لا يتجاوز 100 حرف");

            RuleFor(x => x.SiteDto.Description)
                .MaximumLength(500).WithMessage("الوصف لا يتجاوز 500 حرف");

            GeoRules.CoordinatesRules(this, x => x.SiteDto.Latitude, x => x.SiteDto.Longitude);

            RuleFor(x => x.SiteDto.RegionId)
                .GreaterThan(0).WithMessage("يجب اختيار منطقة");
        }
    }
}
