using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Offices.Queries.GetById
{
    public class GetOfficeByIdQueryHandler
        : IRequestHandler<GetOfficeByIdQuery, OfficeResponseDto>
    {
        private readonly IOfficeService _officeService;
        private readonly IMapper _mapper;

        public GetOfficeByIdQueryHandler(IOfficeService officeService, IMapper mapper)
        {
            _officeService = officeService;
            _mapper = mapper;
        }

        public async Task<OfficeResponseDto> Handle(
            GetOfficeByIdQuery request,
            CancellationToken cancellationToken)
        {
            var office = await _officeService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("المكتب غير موجود");

            return _mapper.Map<OfficeResponseDto>(office);
        }
    }
}
