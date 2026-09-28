using Application.Interfaces;
using MediatR;

namespace Application.Features.Sites.Commands.Delete
{
    public class DeleteSiteCommandHandler : IRequestHandler<DeleteSiteCommand, bool>
    {
        private readonly ISiteService _siteService;

        public DeleteSiteCommandHandler(ISiteService siteService)
        {
            _siteService = siteService;
        }

        public async Task<bool> Handle(DeleteSiteCommand request, CancellationToken cancellationToken)
        {
            var site = await _siteService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("الموقع غير موجود");

            if (await _siteService.HasDeviceLinksAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف موقع مرتبط بأجهزة");

            return await _siteService.DeleteAsync(site);
        }
    }
}
