using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.DeviceCompanies.Commands.Create
{
    public class CreateDeviceCompanyCommandHandler : IRequestHandler<CreateDeviceCompanyCommand, DeviceCompanyResponseDto>
    {
        private readonly IDeviceCompanyService _service;
        private readonly IMapper _mapper;

        public CreateDeviceCompanyCommandHandler(IDeviceCompanyService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<DeviceCompanyResponseDto> Handle(CreateDeviceCompanyCommand request, CancellationToken ct)
        {
            var name = request.Dto.Name.Trim();
            if (await _service.ExistsByNameAsync(name))
                throw new InvalidOperationException("توجد شركة بنفس الاسم مسبقاً");

            var entity = _mapper.Map<DeviceCompany>(request.Dto);
            entity.Name = name;

            return _mapper.Map<DeviceCompanyResponseDto>(await _service.AddAsync(entity));
        }
    }
}
