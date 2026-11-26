using FluentValidation;

namespace Application.Features.DeviceInventory.Sites.Commands.Create
{
    public class CreateSiteCommandValidator : AbstractValidator<CreateSiteCommand>
    {
        public CreateSiteCommandValidator() => RuleFor(x => x.Dto).SetValidator(new SiteRequestValidator());
    }
}
