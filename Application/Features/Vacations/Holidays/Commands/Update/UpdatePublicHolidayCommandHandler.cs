using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.Features.Vacations.Holidays.Commands.Update
{
    public class UpdatePublicHolidayCommandHandler : IRequestHandler<UpdatePublicHolidayCommand, PublicHolidayResponseDto>
    {
        private readonly IPublicHolidayService _service;
        private readonly PublicHolidaySupport _support;

        public UpdatePublicHolidayCommandHandler(IPublicHolidayService service, PublicHolidaySupport support)
        {
            _service = service;
            _support = support;
        }

        /// <summary>تعديل العطلة كلها: الاسم والفترة معاً</summary>
        public async Task<PublicHolidayResponseDto> Handle(UpdatePublicHolidayCommand request, CancellationToken ct)
        {
            var holiday = await LoadAsync(request.Id);
            var start = request.Dto.StartDate.Date;
            var end = (request.Dto.EndDate ?? request.Dto.StartDate).Date;
            await _support.EnsureNoOverlapAsync(start, end, holiday.Id);

            holiday.StartDate = start;
            holiday.EndDate = end;
            holiday.Name = request.Dto.Name.Trim();
            await _service.UpdateAsync(holiday);
            return PublicHolidaySupport.ToDto(holiday);
        }

        private async Task<PublicHoliday> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("العطلة غير موجودة");
    }
}
