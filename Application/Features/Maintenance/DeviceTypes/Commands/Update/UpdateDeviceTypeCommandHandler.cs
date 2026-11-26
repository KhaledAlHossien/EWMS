using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.DeviceTypes.Commands.Update
{
    public class UpdateDeviceTypeCommandHandler : IRequestHandler<UpdateDeviceTypeCommand, DeviceTypeResponseDto>
    {
        private readonly IDeviceTypeService _service;
        private readonly IMapper _mapper;

        public UpdateDeviceTypeCommandHandler(IDeviceTypeService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<DeviceTypeResponseDto> Handle(UpdateDeviceTypeCommand request, CancellationToken ct)
        {
            var entity = await LoadAsync(request.Id);

            var name = request.Dto.Name.Trim();
            if (await _service.ExistsByNameAsync(name, request.Id))
                throw new InvalidOperationException("يوجد نوع جهاز آخر بنفس الاسم");

            _mapper.Map(request.Dto, entity);
            entity.Name = name;
            await _service.UpdateAsync(entity);

            return _mapper.Map<DeviceTypeResponseDto>(entity);
        }

        private async Task<DeviceType> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("نوع الجهاز غير موجود");
    }
}
