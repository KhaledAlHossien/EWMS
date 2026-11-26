using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Queries.GetRequestParts
{
    public class GetRequestPartsQueryHandler : IRequestHandler<GetRequestPartsQuery, RequestPartsDto>
    {
        private readonly IMaintenanceRequestService _requestService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly SparePartSupport _support;

        public GetRequestPartsQueryHandler(IMaintenanceRequestService requestService, IUserService userService, IUserPermissionService permissions, SparePartSupport support)
        {
            _requestService = requestService;
            _userService = userService;
            _permissions = permissions;
            _support = support;
        }

        public async Task<RequestPartsDto> Handle(GetRequestPartsQuery request, CancellationToken ct)
        {
            var r = await LoadRequestAsync(request.RequestId);
            MaintenanceRules.Ensure(
                MaintenanceRules.In(await MaintenanceRules.BoundaryAsync(_userService, _permissions, "ViewMaintenanceRequests"), r),
                "لا يمكنك عرض طلب صيانة خارج نطاقك");
            return await _support.RequestPartsAsync(r);
        }

        private async Task<MaintenanceRequest> LoadRequestAsync(int id) =>
            await _requestService.GetByIdAsync(id) ?? throw new KeyNotFoundException("طلب الصيانة غير موجود");
    }
}
