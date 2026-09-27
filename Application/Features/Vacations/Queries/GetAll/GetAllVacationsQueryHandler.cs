using Application.DTOs.Response;
using Application.Features.Vacations.Query.GetAll;
using Application.Interfaces;
using AutoMapper;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.Vacations.Queries.GetAll
{
    public class GetAllVacationsQueryHandler
       : IRequestHandler<GetAllVacationsQuery, List<VacationResponseDto>>
    {
        private readonly IVacationService _service;
        private readonly IMapper _mapper;

        public GetAllVacationsQueryHandler(IVacationService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<List<VacationResponseDto>> Handle(
            GetAllVacationsQuery request, CancellationToken ct)
        {
            var list = await _service.GetAllAsync();
            return _mapper.Map<List<VacationResponseDto>>(list);
        }
    }
}
