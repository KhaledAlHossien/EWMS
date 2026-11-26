using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.DamageTypes.Commands.Create
{
    public class CreateDamageTypeCommandHandler : IRequestHandler<CreateDamageTypeCommand, DamageTypeResponseDto>
    {
        private readonly IDamageTypeService _service;
        private readonly IMapper _mapper;

        public CreateDamageTypeCommandHandler(IDamageTypeService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<DamageTypeResponseDto> Handle(CreateDamageTypeCommand request, CancellationToken ct)
        {
            var name = request.Dto.Name.Trim();
            if (await _service.ExistsByNameAsync(name))
                throw new InvalidOperationException("يوجد نوع عطل بنفس الاسم مسبقاً");

            var entity = _mapper.Map<DamageType>(request.Dto);
            entity.Name = name;

            return _mapper.Map<DamageTypeResponseDto>(await _service.AddAsync(entity));
        }
    }
}
