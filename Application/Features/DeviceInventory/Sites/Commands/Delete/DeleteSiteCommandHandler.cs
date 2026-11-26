using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory.Sites.Commands.Delete
{
    public class DeleteSiteCommandHandler : IRequestHandler<DeleteSiteCommand, Unit>
    {
        private readonly ISiteService _sites;
        private readonly IDeviceInventoryLogService _log;
        private readonly ICurrentUserService _currentUser;
        private readonly IMapEntryService _entries;
        private readonly IStudyReferenceService _studies;

        public DeleteSiteCommandHandler(ISiteService sites, IDeviceInventoryLogService log, ICurrentUserService currentUser, IMapEntryService entries, IStudyReferenceService studies)
        {
            _sites = sites;
            _log = log;
            _currentUser = currentUser;
            _entries = entries;
            _studies = studies;
        }

        public async Task<Unit> Handle(DeleteSiteCommand request, CancellationToken ct)
        {
            var site = await _sites.GetByIdAsync(request.Id) ?? throw new KeyNotFoundException("الموقع غير موجود");
            if (await _sites.HasDeviceLinksAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف موقع فيه تركيبات — احذفها أو انقلها أولاً");
            if (await _entries.ExistsForSiteAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف موقع عليه معلومات لأحد الفروع (سجلات طبقات) — تُحذف سجلاته أولاً");
            if (await _studies.ExistsForSiteAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف موقع مرتبط بطلبات دراسة");

            await _sites.DeleteAsync(site);
            await _log.AddAsync(Log(DeviceInventoryEntity.Site, site.Id, DeviceInventoryAction.Deleted, _currentUser.UserId, Title(site)));
            return Unit.Value;
        }
    }
}
