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
        IRequestHandler<DeleteSiteCommand, Unit>
    {
        private readonly ISiteService _sites;
        private readonly IDeviceInventoryLogService _log;
        private readonly ICurrentUserService _currentUser;

        public SiteHandler(ISiteService sites, IDeviceInventoryLogService log, ICurrentUserService currentUser)
        {
            _sites = sites;
            _log = log;
            _currentUser = currentUser;
        }

        private async Task<SiteResponseDto> DtoAsync(int id) =>
            ToDto(await _sites.GetWithCountsAsync(id) ?? throw new KeyNotFoundException("الموقع غير موجود"));

        public async Task<List<SiteResponseDto>> Handle(GetAllSitesQuery request, CancellationToken ct) =>
            (await _sites.GetAllWithCountsAsync()).Select(ToDto).ToList();

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
