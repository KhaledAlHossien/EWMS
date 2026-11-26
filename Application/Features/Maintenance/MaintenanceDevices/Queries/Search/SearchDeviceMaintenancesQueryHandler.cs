using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Maintenance.MaintenanceDevices.Queries.Search
{
    public class SearchDeviceMaintenancesQueryHandler : IRequestHandler<SearchDeviceMaintenancesQuery, PagedResultDto<DeviceMaintenanceResponseDto>>
    {
        private readonly IDeviceMaintenanceService _service;
        private readonly IMapper _mapper;

        public SearchDeviceMaintenancesQueryHandler(IDeviceMaintenanceService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<PagedResultDto<DeviceMaintenanceResponseDto>> Handle(SearchDeviceMaintenancesQuery request, CancellationToken ct)
        {
            var (page, pageSize) = MaintenanceRules.NormalizePaging(request.Filter.Page, request.Filter.PageSize);
            var (items, total) = await _service.SearchAsync(request.Filter, page, pageSize);

            return new PagedResultDto<DeviceMaintenanceResponseDto>
            {
                Items = _mapper.Map<List<DeviceMaintenanceResponseDto>>(items),
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }
    }
}
