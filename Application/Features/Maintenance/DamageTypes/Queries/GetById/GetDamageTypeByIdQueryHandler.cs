using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.DamageTypes.Queries.GetById
{
    public class GetDamageTypeByIdQueryHandler : IRequestHandler<GetDamageTypeByIdQuery, DamageTypeResponseDto>
    {
        private readonly IDamageTypeService _service;
        private readonly IMapper _mapper;

        public GetDamageTypeByIdQueryHandler(IDamageTypeService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<DamageTypeResponseDto> Handle(GetDamageTypeByIdQuery request, CancellationToken ct) =>
            _mapper.Map<DamageTypeResponseDto>(await LoadAsync(request.Id));

        private async Task<DamageType> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("نوع العطل غير موجود");
    }
}
