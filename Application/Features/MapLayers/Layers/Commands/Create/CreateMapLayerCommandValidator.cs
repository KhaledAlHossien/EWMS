using FluentValidation;

namespace Application.Features.MapLayers.Layers.Commands.Create
{
    public class CreateMapLayerCommandValidator : AbstractValidator<CreateMapLayerCommand>
    {
        public CreateMapLayerCommandValidator() => RuleFor(x => x.Dto).SetValidator(new MapLayerRequestValidator());
    }
}
