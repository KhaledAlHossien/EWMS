using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Maintenance.DeviceCompanies.Queries.GetAll
{
    public class GetAllDeviceCompaniesQueryHandler : IRequestHandler<GetAllDeviceCompaniesQuery, List<DeviceCompanyResponseDto>>
    {
        private readonly IDeviceCompanyService _service;
        private readonly IMapper _mapper;

        public GetAllDeviceCompaniesQueryHandler(IDeviceCompanyService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<List<DeviceCompanyResponseDto>> Handle(GetAllDeviceCompaniesQuery request, CancellationToken ct) =>
            _mapper.Map<List<DeviceCompanyResponseDto>>(await _service.GetAllAsync());
    }
}
