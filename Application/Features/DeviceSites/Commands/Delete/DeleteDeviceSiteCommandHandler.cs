using Application.Interfaces;
using MediatR;

namespace Application.Features.DeviceSites.Commands.Delete
{
    public class DeleteDeviceSiteCommandHandler : IRequestHandler<DeleteDeviceSiteCommand, bool>
    {
        private readonly IDeviceSiteService _deviceSiteService;

        public DeleteDeviceSiteCommandHandler(IDeviceSiteService deviceSiteService)
        {
            _deviceSiteService = deviceSiteService;
        }

        public async Task<bool> Handle(DeleteDeviceSiteCommand request, CancellationToken cancellationToken)
        {
            var deviceSite = await _deviceSiteService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("الربط غير موجود");

            return await _deviceSiteService.DeleteAsync(deviceSite);
        }
    }
}
