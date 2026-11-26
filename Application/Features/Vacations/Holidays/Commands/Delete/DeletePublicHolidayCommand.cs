using MediatR;

namespace Application.Features.Vacations.Holidays.Commands.Delete
{
    public record DeletePublicHolidayCommand(int Id) : IRequest<Unit>;
}
