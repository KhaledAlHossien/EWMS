using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Vacations.Holidays.Queries.GetAll
{
    public class GetAllPublicHolidaysQueryHandler : IRequestHandler<GetAllPublicHolidaysQuery, List<PublicHolidayResponseDto>>
    {
        private readonly IPublicHolidayService _service;

        public GetAllPublicHolidaysQueryHandler(IPublicHolidayService service)
        {
            _service = service;
        }

        public async Task<List<PublicHolidayResponseDto>> Handle(GetAllPublicHolidaysQuery request, CancellationToken ct) =>
            (await _service.GetAllAsync(request.Year)).Select(PublicHolidaySupport.ToDto).ToList();
    }
}
