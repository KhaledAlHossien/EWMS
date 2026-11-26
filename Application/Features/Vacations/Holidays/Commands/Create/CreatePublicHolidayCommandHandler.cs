using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.Features.Vacations.Holidays.Commands.Create
{
    public class CreatePublicHolidayCommandHandler : IRequestHandler<CreatePublicHolidayCommand, PublicHolidayResponseDto>
    {
        private readonly IPublicHolidayService _service;
        private readonly PublicHolidaySupport _support;

        public CreatePublicHolidayCommandHandler(IPublicHolidayService service, PublicHolidaySupport support)
        {
            _service = service;
            _support = support;
        }

        public async Task<PublicHolidayResponseDto> Handle(CreatePublicHolidayCommand request, CancellationToken ct)
        {
            var start = request.Dto.StartDate.Date;
            var end = (request.Dto.EndDate ?? request.Dto.StartDate).Date;
            await _support.EnsureNoOverlapAsync(start, end);

            var holiday = new PublicHoliday { StartDate = start, EndDate = end, Name = request.Dto.Name.Trim() };
            await _service.AddAsync(holiday);
            return PublicHolidaySupport.ToDto(holiday);
        }
    }
}
