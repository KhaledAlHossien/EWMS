using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory.Sites.Commands.Update
{
    public class UpdateSiteCommandHandler : IRequestHandler<UpdateSiteCommand, SiteResponseDto>
    {
        private readonly ISiteService _sites;
        private readonly IDeviceInventoryLogService _log;
        private readonly ICurrentUserService _currentUser;
        private readonly SiteSupport _support;

        public UpdateSiteCommandHandler(ISiteService sites, IDeviceInventoryLogService log, ICurrentUserService currentUser, SiteSupport support)
        {
            _sites = sites;
            _log = log;
            _currentUser = currentUser;
            _support = support;
        }

        public async Task<SiteResponseDto> Handle(UpdateSiteCommand request, CancellationToken ct)
        {
            var site = await _sites.GetByIdAsync(request.Id) ?? throw new KeyNotFoundException("الموقع غير موجود");
            if (await _sites.ExistsByNameAsync(Clean(request.Dto.Name), request.Id))
                throw new InvalidOperationException("يوجد موقع آخر بنفس الاسم");

            var before = new Site
            {
                Name = site.Name, Description = site.Description, Latitude = site.Latitude, Longitude = site.Longitude,
                GovernorateCode = site.GovernorateCode, ContactName = site.ContactName, ContactPhone = site.ContactPhone, ResponsibleParty = site.ResponsibleParty
            };
            SiteSupport.Apply(request.Dto, site);
            await _sites.UpdateAsync(site, request.Dto.RowVersion);

            var diff = new Diff()
                .Add("الاسم", before.Name, site.Name)
                .Add("الوصف", before.Description, site.Description)
                .Add("خط العرض", before.Latitude, site.Latitude)
                .Add("خط الطول", before.Longitude, site.Longitude)
                .Add("المحافظة", Governorates.NameOf(before.GovernorateCode), Governorates.NameOf(site.GovernorateCode))
                .Add("المسؤول", before.ContactName, site.ContactName)
                .Add("هاتف المسؤول", before.ContactPhone, site.ContactPhone)
                .Add("الجهة المسؤولة", before.ResponsibleParty, site.ResponsibleParty);
            if (diff.Any)
                await _log.AddAsync(Log(DeviceInventoryEntity.Site, site.Id, DeviceInventoryAction.Updated, _currentUser.UserId, Title(site), diff.ToString()));

            return await _support.DtoAsync(site.Id);
        }
    }
}
