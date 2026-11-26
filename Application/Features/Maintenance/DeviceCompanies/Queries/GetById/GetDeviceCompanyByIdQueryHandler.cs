using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.DeviceCompanies.Queries.GetById
{
    public class GetDeviceCompanyByIdQueryHandler : IRequestHandler<GetDeviceCompanyByIdQuery, DeviceCompanyResponseDto>
    {
        private readonly IDeviceCompanyService _service;
        private readonly IMapper _mapper;

        public GetDeviceCompanyByIdQueryHandler(IDeviceCompanyService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<DeviceCompanyResponseDto> Handle(GetDeviceCompanyByIdQuery request, CancellationToken ct) =>
            _mapper.Map<DeviceCompanyResponseDto>(await LoadAsync(request.Id));

        private async Task<DeviceCompany> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("الشركة المصنعة غير موجودة");
    }
}
