using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.DeviceTypes.Queries.GetById
{
    public class GetDeviceTypeByIdQueryHandler : IRequestHandler<GetDeviceTypeByIdQuery, DeviceTypeResponseDto>
    {
        private readonly IDeviceTypeService _service;
        private readonly IMapper _mapper;

        public GetDeviceTypeByIdQueryHandler(IDeviceTypeService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<DeviceTypeResponseDto> Handle(GetDeviceTypeByIdQuery request, CancellationToken ct) =>
            _mapper.Map<DeviceTypeResponseDto>(await LoadAsync(request.Id));

        private async Task<DeviceType> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("نوع الجهاز غير موجود");
    }
}
