using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Offices.Queries.GetAll
{
    public class GetAllOfficesQueryHandler
        : IRequestHandler<GetAllOfficesQuery, List<OfficeResponseDto>>
    {
        private readonly IOfficeService _officeService;
        private readonly IMapper _mapper;

        public GetAllOfficesQueryHandler(IOfficeService officeService, IMapper mapper)
        {
            _officeService = officeService;
            _mapper = mapper;
        }

        public async Task<List<OfficeResponseDto>> Handle(
            GetAllOfficesQuery request,
            CancellationToken cancellationToken)
        {
            var offices = await _officeService.GetAllAsync();
            return _mapper.Map<List<OfficeResponseDto>>(offices);
        }
    }
}
