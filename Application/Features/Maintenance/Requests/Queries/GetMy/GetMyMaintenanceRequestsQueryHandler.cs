using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Maintenance.Requests.Queries.GetMy
{
    public class GetMyMaintenanceRequestsQueryHandler : IRequestHandler<GetMyMaintenanceRequestsQuery, List<MyMaintenanceRequestDto>>
    {
        private readonly IMaintenanceRequestService _requestService;
        private readonly IUserService _userService;

        public GetMyMaintenanceRequestsQueryHandler(IMaintenanceRequestService requestService, IUserService userService)
        {
            _requestService = requestService;
            _userService = userService;
        }

        public async Task<List<MyMaintenanceRequestDto>> Handle(GetMyMaintenanceRequestsQuery request, CancellationToken ct)
        {
            var me = await MaintenanceRules.CurrentUserAsync(_userService);
            return (await _requestService.GetForClientAsync(me.Id, 100)).Select(r => new MyMaintenanceRequestDto
            {
                Id = r.Id,
                Number = MaintenanceRules.RequestNumber(r.Id, r.CreatedAt),
                DeviceName = r.DeviceMaintenance?.Name ?? "",
                DeviceTypeName = r.DeviceMaintenance?.DeviceType?.Name ?? "",
                DeviceCompanyName = r.DeviceMaintenance?.DeviceCompany?.Name ?? "",
                Model = r.DeviceMaintenance?.Model ?? "",
                SerialNumber = r.DeviceMaintenance?.SerialNumber ?? "",
                DamageTypeName = r.DamageType?.Name ?? "",
                StatusName = r.MaintenanceRequestStatus?.Name ?? "",
                StatusColor = r.MaintenanceRequestStatus?.Color ?? "",
                TechnicianName = r.User?.FullName ?? "",
                CreatedAt = r.CreatedAt,
                CompletedAt = r.CompletedAt,
                DeliveredAt = r.DeliveredAt
            }).ToList();
        }
    }
}
