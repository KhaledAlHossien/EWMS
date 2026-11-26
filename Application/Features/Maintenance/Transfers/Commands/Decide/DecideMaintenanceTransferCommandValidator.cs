using FluentValidation;

namespace Application.Features.Maintenance.Transfers.Commands.Decide
{
    public class DecideMaintenanceTransferCommandValidator : AbstractValidator<DecideMaintenanceTransferCommand>
    {
        public DecideMaintenanceTransferCommandValidator()
        {
            RuleFor(x => x.Dto.Note).MaximumLength(500).WithMessage("الملاحظة لا تتجاوز 500 حرف");
        }
    }
}
