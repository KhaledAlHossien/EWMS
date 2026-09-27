using Application.DTOs.Response;
using Application.Features.Vacations.Query.GetById;
using Application.Interfaces;
using AutoMapper;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.Vacations.Queries.GetById
{
    public class GetVacationByIdQueryHandler
        : IRequestHandler<GetVacationByIdQuery, VacationResponseDto>
    {
        private readonly IVacationService _service;
        private readonly IMapper _mapper;

        public GetVacationByIdQueryHandler(IVacationService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<VacationResponseDto> Handle(
            GetVacationByIdQuery request, CancellationToken ct)
        {
            var vacation = await _service.GetWithDetailsAsync(request.Id)
                ?? throw new KeyNotFoundException("الإجازة غير موجودة");

            return _mapper.Map<VacationResponseDto>(vacation);
        }
    }
}
