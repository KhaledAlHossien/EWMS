using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Vacations.Holidays.Commands.Create
{
    public record CreatePublicHolidayCommand(PublicHolidayRequestDto Dto) : IRequest<PublicHolidayResponseDto>;
}
