using FluentValidation;

namespace Application.Features.MapLayers.Layers.Commands.Update
{
    public class UpdateMapLayerCommandValidator : AbstractValidator<UpdateMapLayerCommand>
    {
        public UpdateMapLayerCommandValidator() => RuleFor(x => x.Dto).SetValidator(new MapLayerRequestValidator());
    }
}
