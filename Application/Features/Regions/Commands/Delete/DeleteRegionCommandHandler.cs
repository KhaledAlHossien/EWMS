using Application.Interfaces;
using MediatR;

namespace Application.Features.Regions.Commands.Delete
{
    public class DeleteRegionCommandHandler : IRequestHandler<DeleteRegionCommand, bool>
    {
        private readonly IRegionService _regionService;

        public DeleteRegionCommandHandler(IRegionService regionService)
        {
            _regionService = regionService;
        }

        public async Task<bool> Handle(DeleteRegionCommand request, CancellationToken cancellationToken)
        {
            var region = await _regionService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("المنطقة غير موجودة");

            if (await _regionService.HasSitesAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف منطقة تحتوي على مواقع");

            return await _regionService.DeleteAsync(region);
        }
    }
}
