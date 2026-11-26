using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory.Sites.Commands.Create
{
    public class CreateSiteCommandHandler : IRequestHandler<CreateSiteCommand, SiteResponseDto>
    {
        private readonly ISiteService _sites;
        private readonly IDeviceInventoryLogService _log;
        private readonly ICurrentUserService _currentUser;
        private readonly SiteSupport _support;

        public CreateSiteCommandHandler(ISiteService sites, IDeviceInventoryLogService log, ICurrentUserService currentUser, SiteSupport support)
        {
            _sites = sites;
            _log = log;
            _currentUser = currentUser;
            _support = support;
        }

        public async Task<SiteResponseDto> Handle(CreateSiteCommand request, CancellationToken ct)
        {
            if (await _sites.ExistsByNameAsync(Clean(request.Dto.Name)))
                throw new InvalidOperationException("يوجد موقع بنفس الاسم مسبقاً");

            var site = new Site();
            SiteSupport.Apply(request.Dto, site);
            await _sites.AddAsync(site);

            await _log.AddAsync(Log(DeviceInventoryEntity.Site, site.Id, DeviceInventoryAction.Created, _currentUser.UserId, Title(site),
                $"المحافظة: {Governorates.NameOf(site.GovernorateCode)}"));
            return await _support.DtoAsync(site.Id);
        }
    }
}
