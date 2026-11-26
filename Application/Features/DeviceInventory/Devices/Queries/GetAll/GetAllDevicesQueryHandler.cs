using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory.Devices.Queries.GetAll
{
    public class GetAllDevicesQueryHandler : IRequestHandler<GetAllDevicesQuery, List<DeviceResponseDto>>
    {
        private readonly IDeviceService _devices;

        public GetAllDevicesQueryHandler(IDeviceService devices)
        {
            _devices = devices;
        }

        public async Task<List<DeviceResponseDto>> Handle(GetAllDevicesQuery request, CancellationToken ct) =>
            (await _devices.GetAllWithCountsAsync()).Select(ToDto).ToList();
    }
}
