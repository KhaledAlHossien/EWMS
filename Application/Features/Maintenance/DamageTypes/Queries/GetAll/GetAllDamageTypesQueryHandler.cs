using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Maintenance.DamageTypes.Queries.GetAll
{
    public class GetAllDamageTypesQueryHandler : IRequestHandler<GetAllDamageTypesQuery, List<DamageTypeResponseDto>>
    {
        private readonly IDamageTypeService _service;
        private readonly IMapper _mapper;

        public GetAllDamageTypesQueryHandler(IDamageTypeService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<List<DamageTypeResponseDto>> Handle(GetAllDamageTypesQuery request, CancellationToken ct) =>
            _mapper.Map<List<DamageTypeResponseDto>>(await _service.GetAllAsync());
    }
}
