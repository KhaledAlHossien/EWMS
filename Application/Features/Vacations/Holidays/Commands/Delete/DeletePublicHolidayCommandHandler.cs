using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.Features.Vacations.Holidays.Commands.Delete
{
    public class DeletePublicHolidayCommandHandler : IRequestHandler<DeletePublicHolidayCommand, Unit>
    {
        private readonly IPublicHolidayService _service;

        public DeletePublicHolidayCommandHandler(IPublicHolidayService service)
        {
            _service = service;
        }

        public async Task<Unit> Handle(DeletePublicHolidayCommand request, CancellationToken ct)
        {
            await _service.DeleteAsync(await LoadAsync(request.Id));
            return Unit.Value;
        }

        private async Task<PublicHoliday> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("العطلة غير موجودة");
    }
}
