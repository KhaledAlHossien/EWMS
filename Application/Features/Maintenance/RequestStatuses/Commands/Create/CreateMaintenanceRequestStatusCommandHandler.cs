using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.RequestStatuses.Commands.Create
{
    public class CreateMaintenanceRequestStatusCommandHandler : IRequestHandler<CreateMaintenanceRequestStatusCommand, MaintenanceRequestStatusResponseDto>
    {
        private readonly IMaintenanceRequestStatusService _service;
        private readonly IMapper _mapper;

        public CreateMaintenanceRequestStatusCommandHandler(IMaintenanceRequestStatusService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<MaintenanceRequestStatusResponseDto> Handle(CreateMaintenanceRequestStatusCommand request, CancellationToken ct)
        {
            var name = request.Dto.Name.Trim();
            if (await _service.ExistsByNameAsync(name))
                throw new InvalidOperationException("توجد حالة بنفس الاسم مسبقاً");

            var entity = _mapper.Map<MaintenanceRequestStatus>(request.Dto);
            entity.Name = name;
            entity.Color = request.Dto.Color.ToUpperInvariant();

            return _mapper.Map<MaintenanceRequestStatusResponseDto>(await _service.AddAsync(entity));
        }
    }
}
