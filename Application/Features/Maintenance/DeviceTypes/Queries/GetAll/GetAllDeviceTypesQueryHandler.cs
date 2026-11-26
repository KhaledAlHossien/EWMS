using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Maintenance.DeviceTypes.Queries.GetAll
{
    public class GetAllDeviceTypesQueryHandler : IRequestHandler<GetAllDeviceTypesQuery, List<DeviceTypeResponseDto>>
    {
        private readonly IDeviceTypeService _service;
        private readonly IMapper _mapper;

        public GetAllDeviceTypesQueryHandler(IDeviceTypeService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<List<DeviceTypeResponseDto>> Handle(GetAllDeviceTypesQuery request, CancellationToken ct) =>
            _mapper.Map<List<DeviceTypeResponseDto>>(await _service.GetAllAsync());
    }
}
