using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Queries.GetMovements
{
    public class GetSparePartMovementsQueryHandler : IRequestHandler<GetSparePartMovementsQuery, PagedResultDto<SparePartMovementDto>>
    {
        private readonly ISparePartService _service;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly SparePartSupport _support;

        public GetSparePartMovementsQueryHandler(ISparePartService service, IUserService userService, IUserPermissionService permissions, SparePartSupport support)
        {
            _service = service;
            _userService = userService;
            _permissions = permissions;
            _support = support;
        }

        public async Task<PagedResultDto<SparePartMovementDto>> Handle(GetSparePartMovementsQuery request, CancellationToken ct)
        {
            var part = await _support.LoadInScopeAsync(request.Id, await ViewerAsync());
            var (page, pageSize) = MaintenanceRules.NormalizePaging(request.Page, request.PageSize);
            var (items, total) = await _service.GetMovementsAsync(part.Id, page, pageSize);

            return new PagedResultDto<SparePartMovementDto>
            {
                Items = items.Select(m => new SparePartMovementDto
                {
                    Id = m.Id,
                    Type = (int)m.Type,
                    TypeAr = SparePartRules.MovementAr(m.Type),
                    Quantity = m.Quantity,
                    UnitCost = m.UnitCost,
                    BalanceAfter = m.BalanceAfter,
                    Date = m.Date,
                    Note = m.Note,
                    MaintenanceRequestId = m.MaintenanceRequestId,
                    RequestNumber = m.MaintenanceRequest != null ? MaintenanceRules.RequestNumber(m.MaintenanceRequest.Id, m.MaintenanceRequest.CreatedAt) : null,
                    UserName = m.User?.FullName ?? string.Empty,
                    CreatedAt = m.CreatedAt
                }).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
