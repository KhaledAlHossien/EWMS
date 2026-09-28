using Application.Interfaces;
using MediatR;

namespace Application.Features.Devices.Commands.Delete
{
    public class DeleteDeviceCommandHandler : IRequestHandler<DeleteDeviceCommand, bool>
    {
        private readonly IDeviceService _deviceService;

        public DeleteDeviceCommandHandler(IDeviceService deviceService)
        {
            _deviceService = deviceService;
        }

        public async Task<bool> Handle(DeleteDeviceCommand request, CancellationToken cancellationToken)
        {
            var device = await _deviceService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("الجهاز غير موجود");

            if (await _deviceService.HasSiteLinksAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف جهاز مرتبط بمواقع");

            return await _deviceService.DeleteAsync(device);
        }
    }
}
