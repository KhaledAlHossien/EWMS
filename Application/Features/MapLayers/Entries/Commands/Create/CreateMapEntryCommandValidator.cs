using FluentValidation;

namespace Application.Features.MapLayers.Entries.Commands.Create
{
    public class CreateMapEntryCommandValidator : AbstractValidator<CreateMapEntryCommand>
    {
        public CreateMapEntryCommandValidator()
        {
            RuleFor(x => x.Dto.LayerId).GreaterThan(0).WithMessage("اختر الطبقة");
            RuleFor(x => x.Dto.SiteId).GreaterThan(0).WithMessage("اختر الموقع");
        }
    }
}
