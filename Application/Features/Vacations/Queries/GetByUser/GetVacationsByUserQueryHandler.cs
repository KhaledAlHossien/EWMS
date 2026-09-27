using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.Vacations.Queries.GetByUser
{
    public class GetVacationsByUserQueryHandler
         : IRequestHandler<GetVacationsByUserQuery, List<VacationResponseDto>>
    {
        private readonly IVacationService _service;
        private readonly IMapper _mapper;

        public GetVacationsByUserQueryHandler(IVacationService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<List<VacationResponseDto>> Handle(
            GetVacationsByUserQuery request, CancellationToken ct)
        {
            var list = await _service.GetByUserIdAsync(request.UserId);
            return _mapper.Map<List<VacationResponseDto>>(list);
        }
    }
}
