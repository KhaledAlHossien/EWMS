using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.VacationTypes.Queries.GetAll
{
    public class GetAllVacationTypesQueryHandler
         : IRequestHandler<GetAllVacationTypesQuery, List<VacationTypeResponseDto>>
    {
        private readonly IVacationTypeService _service;
        private readonly IMapper _mapper;

        public GetAllVacationTypesQueryHandler(
            IVacationTypeService service,
            IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<List<VacationTypeResponseDto>> Handle(
            GetAllVacationTypesQuery request, CancellationToken ct)
        {
            var list = await _service.GetAllAsync();
            return _mapper.Map<List<VacationTypeResponseDto>>(list);
        }
    }
}
