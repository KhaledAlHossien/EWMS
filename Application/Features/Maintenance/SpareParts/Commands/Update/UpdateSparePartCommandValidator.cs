using FluentValidation;

namespace Application.Features.Maintenance.SpareParts.Commands.Update
{
    public class UpdateSparePartCommandValidator : AbstractValidator<UpdateSparePartCommand>
    {
        public UpdateSparePartCommandValidator() => RuleFor(x => x.Dto).SetValidator(new SparePartRequestDtoValidator());
    }
}
