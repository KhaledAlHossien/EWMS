using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory.Devices.Queries.GetById
{
    public class GetDeviceByIdQueryHandler : IRequestHandler<GetDeviceByIdQuery, DeviceResponseDto>
    {
        private readonly IDeviceService _devices;

        public GetDeviceByIdQueryHandler(IDeviceService devices)
        {
            _devices = devices;
        }

        public Task<DeviceResponseDto> Handle(GetDeviceByIdQuery request, CancellationToken ct) => DtoAsync(request.Id);

        private async Task<DeviceResponseDto> DtoAsync(int id) =>
            ToDto(await _devices.GetWithCountAsync(id) ?? throw new KeyNotFoundException("الجهاز غير موجود"));
    }
}
