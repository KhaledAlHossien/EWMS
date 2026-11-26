using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Queries.GetAvailablePartsForRequest
{
    public class GetAvailablePartsForRequestQueryHandler : IRequestHandler<GetAvailablePartsForRequestQuery, List<SparePartResponseDto>>
    {
        private readonly ISparePartService _service;
        private readonly IMaintenanceRequestService _requestService;
        private readonly SparePartSupport _support;

        public GetAvailablePartsForRequestQueryHandler(ISparePartService service, IMaintenanceRequestService requestService, SparePartSupport support)
        {
            _service = service;
            _requestService = requestService;
            _support = support;
        }

        public async Task<List<SparePartResponseDto>> Handle(GetAvailablePartsForRequestQuery request, CancellationToken ct)
        {
            var r = await LoadRequestAsync(request.RequestId);
            await _support.EnsureCanIssueAsync(r);

            var parts = await _service.GetAvailableForRequestAsync(r.DepartmentId!.Value,
                r.DeviceMaintenance.DeviceTypeId, r.DeviceMaintenance.DeviceCompanyId, request.Search, 30);
            return parts.Select(SparePartRules.ToDto).ToList();
        }

        private async Task<MaintenanceRequest> LoadRequestAsync(int id) =>
            await _requestService.GetByIdAsync(id) ?? throw new KeyNotFoundException("طلب الصيانة غير موجود");
    }
}
