using FluentValidation;

namespace Application.Features.Maintenance.MaintenanceDevices.Queries.GetBySerial
{
    public class GetDeviceMaintenanceBySerialQueryValidator : AbstractValidator<GetDeviceMaintenanceBySerialQuery>
    {
        public GetDeviceMaintenanceBySerialQueryValidator() =>
            RuleFor(x => x.SerialNumber).NotEmpty().WithMessage("اكتب الرقم التسلسلي");
    }
}
