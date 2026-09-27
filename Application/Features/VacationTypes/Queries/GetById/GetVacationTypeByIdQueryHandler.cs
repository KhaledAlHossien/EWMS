using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.VacationTypes.Queries.GetById
{
    public class GetVacationTypeByIdQueryHandler
        : IRequestHandler<GetVacationTypeByIdQuery, VacationTypeResponseDto>
    {
        private readonly IVacationTypeService _service;
        private readonly IMapper _mapper;

        public GetVacationTypeByIdQueryHandler(
            IVacationTypeService service,
            IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<VacationTypeResponseDto> Handle(
            GetVacationTypeByIdQuery request, CancellationToken ct)
        {
            var entity = await _service.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("نوع الإجازة غير موجود");

            return _mapper.Map<VacationTypeResponseDto>(entity);
        }
    }
}