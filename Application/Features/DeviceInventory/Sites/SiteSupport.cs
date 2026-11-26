using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory.Sites
{
    /// <summary>الدوال المشتركة بين عمليات هذه الميزة</summary>
    public class SiteSupport
    {
        private readonly ISiteService _sites;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;

        public SiteSupport(ISiteService sites, IUserService users, IUserPermissionService permissions)
        {
            _sites = sites;
            _users = users;
            _permissions = permissions;
        }

        /// <summary>عدّادات التركيبات ضمن حدّ المستخدم فقط (السجل نفسه مشترك)</summary>
        public async Task<InstallationScope> ScopeAsync() => ScopeFor(await Viewer.CurrentAsync(_users, _permissions));

        public async Task<SiteResponseDto> DtoAsync(int id) =>
            ToDto(await _sites.GetWithCountsAsync(id, await ScopeAsync()) ?? throw new KeyNotFoundException("الموقع غير موجود"));

        /// <summary>القيم المنظّفة كما تُحفظ (قصّ الفراغات، الهاتف بلا فراغات، المحافظة من الإحداثيات)</summary>
        public static void Apply(SiteRequestDto dto, Site site)
        {
            site.Name = Clean(dto.Name);
            site.Description = Clean(dto.Description);
            site.Latitude = dto.Latitude;
            site.Longitude = dto.Longitude;
            site.GovernorateCode = GovernorateResolver.CodeFor(dto.Latitude, dto.Longitude);
            site.ContactName = Clean(dto.ContactName);
            site.ContactPhone = PhoneRules.ToStored(dto.ContactPhone) ?? string.Empty;
            site.ResponsibleParty = Clean(dto.ResponsibleParty);
        }
    }
}
