using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Vacations.Holidays.Commands.Update
{
    public record UpdatePublicHolidayCommand(int Id, PublicHolidayRequestDto Dto) : IRequest<PublicHolidayResponseDto>;
}
