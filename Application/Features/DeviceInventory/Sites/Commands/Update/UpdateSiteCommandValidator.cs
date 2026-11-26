using FluentValidation;

namespace Application.Features.DeviceInventory.Sites.Commands.Update
{
    public class UpdateSiteCommandValidator : AbstractValidator<UpdateSiteCommand>
    {
        public UpdateSiteCommandValidator() => RuleFor(x => x.Dto).SetValidator(new SiteRequestValidator());
    }
}
