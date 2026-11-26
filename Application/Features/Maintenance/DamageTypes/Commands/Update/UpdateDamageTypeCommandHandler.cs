using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.DamageTypes.Commands.Update
{
    public class UpdateDamageTypeCommandHandler : IRequestHandler<UpdateDamageTypeCommand, DamageTypeResponseDto>
    {
        private readonly IDamageTypeService _service;
        private readonly IMapper _mapper;

        public UpdateDamageTypeCommandHandler(IDamageTypeService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<DamageTypeResponseDto> Handle(UpdateDamageTypeCommand request, CancellationToken ct)
        {
            var entity = await LoadAsync(request.Id);

            var name = request.Dto.Name.Trim();
            if (await _service.ExistsByNameAsync(name, request.Id))
                throw new InvalidOperationException("يوجد نوع عطل آخر بنفس الاسم");

            _mapper.Map(request.Dto, entity);
            entity.Name = name;
            await _service.UpdateAsync(entity);

            return _mapper.Map<DamageTypeResponseDto>(entity);
        }

        private async Task<DamageType> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("نوع العطل غير موجود");
    }
}
