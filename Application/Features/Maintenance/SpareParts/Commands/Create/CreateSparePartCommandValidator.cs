using FluentValidation;

namespace Application.Features.Maintenance.SpareParts.Commands.Create
{
    public class CreateSparePartCommandValidator : AbstractValidator<CreateSparePartCommand>
    {
        public CreateSparePartCommandValidator() => RuleFor(x => x.Dto).SetValidator(new SparePartRequestDtoValidator());
    }
}
