using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.RequestStatuses.Queries.GetById
{
    public class GetMaintenanceRequestStatusByIdQueryHandler : IRequestHandler<GetMaintenanceRequestStatusByIdQuery, MaintenanceRequestStatusResponseDto>
    {
        private readonly IMaintenanceRequestStatusService _service;
        private readonly IMapper _mapper;

        public GetMaintenanceRequestStatusByIdQueryHandler(IMaintenanceRequestStatusService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<MaintenanceRequestStatusResponseDto> Handle(GetMaintenanceRequestStatusByIdQuery request, CancellationToken ct) =>
            _mapper.Map<MaintenanceRequestStatusResponseDto>(await LoadAsync(request.Id));

        private async Task<MaintenanceRequestStatus> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("حالة الطلب غير موجودة");
    }
}
