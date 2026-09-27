using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using MediatR;

namespace Application.Features.VacationTypes.Commands.Create
{
    public class CreateVacationTypeCommandHandler
        : IRequestHandler<CreateVacationTypeCommand, VacationTypeResponseDto>
    {
        private readonly IVacationTypeService _service;
        private readonly IMapper _mapper;

        public CreateVacationTypeCommandHandler(
            IVacationTypeService service,
            IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<VacationTypeResponseDto> Handle(
            CreateVacationTypeCommand request, CancellationToken ct)
        {
            var dto = request.VacationTypeDto;

            // تحقق من عدم تكرار الاسم
            if (!await _service.IsNameUniqueAsync(dto.Name))
                throw new InvalidOperationException(
                    $"يوجد نوع إجازة بنفس الاسم: {dto.Name}");

            var entity = new VacationType
            {
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim() ?? string.Empty
            };

            await _service.AddAsync(entity);
            return _mapper.Map<VacationTypeResponseDto>(entity);
        }
    }
}