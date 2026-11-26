using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.DeviceCompanies.Commands.Update
{
    public class UpdateDeviceCompanyCommandHandler : IRequestHandler<UpdateDeviceCompanyCommand, DeviceCompanyResponseDto>
    {
        private readonly IDeviceCompanyService _service;
        private readonly IMapper _mapper;

        public UpdateDeviceCompanyCommandHandler(IDeviceCompanyService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<DeviceCompanyResponseDto> Handle(UpdateDeviceCompanyCommand request, CancellationToken ct)
        {
            var entity = await LoadAsync(request.Id);

            var name = request.Dto.Name.Trim();
            if (await _service.ExistsByNameAsync(name, request.Id))
                throw new InvalidOperationException("توجد شركة أخرى بنفس الاسم");

            _mapper.Map(request.Dto, entity);
            entity.Name = name;
            await _service.UpdateAsync(entity);

            return _mapper.Map<DeviceCompanyResponseDto>(entity);
        }

        private async Task<DeviceCompany> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("الشركة المصنعة غير موجودة");
    }
}
