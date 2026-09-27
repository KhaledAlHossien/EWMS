using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.VacationTypes.Commands.Update
{
    public class UpdateVacationTypeCommandHandler
        : IRequestHandler<UpdateVacationTypeCommand, VacationTypeResponseDto>
    {
        private readonly IVacationTypeService _service;
        private readonly IMapper _mapper;

        public UpdateVacationTypeCommandHandler(
            IVacationTypeService service,
            IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<VacationTypeResponseDto> Handle(
            UpdateVacationTypeCommand request, CancellationToken ct)
        {
            var entity = await _service.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("نوع الإجازة غير موجود");

            var dto = request.VacationTypeDto;

            // تحقق من عدم تكرار الاسم (مع استثناء نفسه)
            if (!await _service.IsNameUniqueAsync(dto.Name, request.Id))
                throw new InvalidOperationException(
                    $"يوجد نوع إجازة بنفس الاسم: {dto.Name}");

            entity.Name = dto.Name.Trim();
            entity.Description = dto.Description?.Trim() ?? string.Empty;

            await _service.UpdateAsync(entity);
            return _mapper.Map<VacationTypeResponseDto>(entity);
        }
    }
}