using Application.Common;
using Application.DTOs.Response;
using Application.Features.DeviceInventory.Installations;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory.Queries.GetHistory
{
    public class GetDeviceInventoryHistoryQueryHandler : IRequestHandler<GetDeviceInventoryHistoryQuery, PagedResultDto<DeviceInventoryLogDto>>
    {
        private readonly IDeviceInventoryLogService _log;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly InstallationSupport _support;

        public GetDeviceInventoryHistoryQueryHandler(IDeviceInventoryLogService log, IUserService users, IUserPermissionService permissions, InstallationSupport support)
        {
            _log = log;
            _users = users;
            _permissions = permissions;
            _support = support;
        }

        public async Task<PagedResultDto<DeviceInventoryLogDto>> Handle(GetDeviceInventoryHistoryQuery request, CancellationToken ct)
        {
            // سجل التركيب لمن يراه فقط؛ سجل الموقع والجهاز (مشتركان) لكل من يدخل الواجهة
            if (request.Type == DeviceInventoryEntity.Installation)
                await _support.LoadInScopeAsync(request.Id, await CurrentAsync());
            var (page, pageSize) = Paging(request.Page, request.PageSize);
            var (items, total) = await _log.GetAsync(request.Type, request.Id, page, pageSize);
            return new PagedResultDto<DeviceInventoryLogDto> { Items = items.Select(ToDto).ToList(), TotalCount = total, Page = page, PageSize = pageSize };
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
