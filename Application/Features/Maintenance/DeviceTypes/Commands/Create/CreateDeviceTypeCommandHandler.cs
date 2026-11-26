using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.DeviceTypes.Commands.Create
{
    public class CreateDeviceTypeCommandHandler : IRequestHandler<CreateDeviceTypeCommand, DeviceTypeResponseDto>
    {
        private readonly IDeviceTypeService _service;
        private readonly IMapper _mapper;

        public CreateDeviceTypeCommandHandler(IDeviceTypeService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<DeviceTypeResponseDto> Handle(CreateDeviceTypeCommand request, CancellationToken ct)
        {
            var name = request.Dto.Name.Trim();
            if (await _service.ExistsByNameAsync(name))
                throw new InvalidOperationException("يوجد نوع جهاز بنفس الاسم مسبقاً");

            var entity = _mapper.Map<DeviceType>(request.Dto);
            entity.Name = name;

            return _mapper.Map<DeviceTypeResponseDto>(await _service.AddAsync(entity));
        }
    }
}
