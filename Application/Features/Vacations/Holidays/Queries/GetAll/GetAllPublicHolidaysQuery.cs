using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Vacations.Holidays.Queries.GetAll
{
    public record GetAllPublicHolidaysQuery(int? Year) : IRequest<List<PublicHolidayResponseDto>>;
}
