using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;
using MediatR;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory
{
    public record GetAllSitesQuery : IRequest<List<SiteResponseDto>>;
    public record GetSiteByIdQuery(int Id) : IRequest<SiteResponseDto>;
    public record CreateSiteCommand(SiteRequestDto Dto) : IRequest<SiteResponseDto>;
    public record UpdateSiteCommand(int Id, SiteRequestDto Dto) : IRequest<SiteResponseDto>;
    public record DeleteSiteCommand(int Id) : IRequest<Unit>;
    /// <summary>مواقع قريبة من نقطة (تنبيه قبل إضافة موقع مكرر في السجل المشترك)</summary>
    public record GetNearbySitesQuery(double Latitude, double Longitude, int? ExcludeId) : IRequest<List<NearbySiteDto>>;

    public class CreateSiteCommandValidator : AbstractValidator<CreateSiteCommand>
    {
        public CreateSiteCommandValidator() => RuleFor(x => x.Dto).SetValidator(new SiteRequestValidator());
    }

    public class UpdateSiteCommandValidator : AbstractValidator<UpdateSiteCommand>
    {
        public UpdateSiteCommandValidator() => RuleFor(x => x.Dto).SetValidator(new SiteRequestValidator());
    }

    public class SiteHandler :
        IRequestHandler<GetAllSitesQuery, List<SiteResponseDto>>,
        IRequestHandler<GetSiteByIdQuery, SiteResponseDto>,
        IRequestHandler<CreateSiteCommand, SiteResponseDto>,
        IRequestHandler<UpdateSiteCommand, SiteResponseDto>,
        IRequestHandler<DeleteSiteCommand, Unit>,
        IRequestHandler<GetNearbySitesQuery, List<NearbySiteDto>>
    {
        /// <summary>ما دون هذه المسافة يُعدّ «موقعاً قريباً» يُنبَّه إليه قبل الإضافة</summary>
        public const double NearbyRadiusMeters = 200;

        private readonly ISiteService _sites;
        private readonly IDeviceInventoryLogService _log;
        private readonly ICurrentUserService _currentUser;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;

        public SiteHandler(ISiteService sites, IDeviceInventoryLogService log, ICurrentUserService currentUser,
            IUserService users, IUserPermissionService permissions)
        {
            _sites = sites;
            _log = log;
            _currentUser = currentUser;
            _users = users;
            _permissions = permissions;
        }

        /// <summary>عدّادات التركيبات ضمن حدّ المستخدم فقط (السجل نفسه مشترك)</summary>
        private async Task<InstallationScope> ScopeAsync() => ScopeFor(await Viewer.CurrentAsync(_users, _permissions));

        private async Task<SiteResponseDto> DtoAsync(int id) =>
            ToDto(await _sites.GetWithCountsAsync(id, await ScopeAsync()) ?? throw new KeyNotFoundException("الموقع غير موجود"));

        public async Task<List<SiteResponseDto>> Handle(GetAllSitesQuery request, CancellationToken ct) =>
            (await _sites.GetAllWithCountsAsync(await ScopeAsync())).Select(ToDto).ToList();

        public async Task<List<NearbySiteDto>> Handle(GetNearbySitesQuery request, CancellationToken ct) =>
            (await _sites.GetNearbyAsync(request.Latitude, request.Longitude, NearbyRadiusMeters, request.ExcludeId))
                .Select(n => new NearbySiteDto { Id = n.Id, Name = n.Name, DistanceMeters = n.DistanceMeters })
                .ToList();

        public Task<SiteResponseDto> Handle(GetSiteByIdQuery request, CancellationToken ct) => DtoAsync(request.Id);

        /// <summary>القيم المنظّفة كما تُحفظ (قصّ الفراغات، الهاتف بلا فراغات، المحافظة من الإحداثيات)</summary>
        private static void Apply(SiteRequestDto dto, Site site)
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

        public async Task<SiteResponseDto> Handle(CreateSiteCommand request, CancellationToken ct)
        {
            if (await _sites.ExistsByNameAsync(Clean(request.Dto.Name)))
                throw new InvalidOperationException("يوجد موقع بنفس الاسم مسبقاً");

            var site = new Site();
            Apply(request.Dto, site);
            await _sites.AddAsync(site);

            await _log.AddAsync(Log(DeviceInventoryEntity.Site, site.Id, DeviceInventoryAction.Created, _currentUser.UserId, Title(site),
                $"المحافظة: {Governorates.NameOf(site.GovernorateCode)}"));
            return await DtoAsync(site.Id);
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
            Apply(request.Dto, site);
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

            return await DtoAsync(site.Id);
        }

        public async Task<Unit> Handle(DeleteSiteCommand request, CancellationToken ct)
        {
            var site = await _sites.GetByIdAsync(request.Id) ?? throw new KeyNotFoundException("الموقع غير موجود");
            if (await _sites.HasDeviceLinksAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف موقع فيه تركيبات — احذفها أو انقلها أولاً");

            await _sites.DeleteAsync(site);
            await _log.AddAsync(Log(DeviceInventoryEntity.Site, site.Id, DeviceInventoryAction.Deleted, _currentUser.UserId, Title(site)));
            return Unit.Value;
        }
    }
}
