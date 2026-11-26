using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.RequestStatuses.Commands.Update
{
    public class UpdateMaintenanceRequestStatusCommandHandler : IRequestHandler<UpdateMaintenanceRequestStatusCommand, MaintenanceRequestStatusResponseDto>
    {
        private readonly IMaintenanceRequestStatusService _service;
        private readonly IMapper _mapper;

        public UpdateMaintenanceRequestStatusCommandHandler(IMaintenanceRequestStatusService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<MaintenanceRequestStatusResponseDto> Handle(UpdateMaintenanceRequestStatusCommand request, CancellationToken ct)
        {
            var entity = await LoadAsync(request.Id);

            var name = request.Dto.Name.Trim();
            if (await _service.ExistsByNameAsync(name, request.Id))
                throw new InvalidOperationException("توجد حالة أخرى بنفس الاسم");

            _mapper.Map(request.Dto, entity);
            entity.Name = name;
            entity.Color = request.Dto.Color.ToUpperInvariant();
            await _service.UpdateAsync(entity);

            return _mapper.Map<MaintenanceRequestStatusResponseDto>(entity);
        }

        private async Task<MaintenanceRequestStatus> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("حالة الطلب غير موجودة");
    }
}
