using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Maintenance.RequestStatuses.Queries.GetAll
{
    public class GetAllMaintenanceRequestStatusesQueryHandler : IRequestHandler<GetAllMaintenanceRequestStatusesQuery, List<MaintenanceRequestStatusResponseDto>>
    {
        private readonly IMaintenanceRequestStatusService _service;
        private readonly IMapper _mapper;

        public GetAllMaintenanceRequestStatusesQueryHandler(IMaintenanceRequestStatusService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<List<MaintenanceRequestStatusResponseDto>> Handle(GetAllMaintenanceRequestStatusesQuery request, CancellationToken ct) =>
            _mapper.Map<List<MaintenanceRequestStatusResponseDto>>(await _service.GetAllAsync());
    }
}
