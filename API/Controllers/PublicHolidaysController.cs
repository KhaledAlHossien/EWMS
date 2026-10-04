using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Vacations.Holidays;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>العطل الرسمية — لا تُحسب من مدة الإجازة (مع الجمعة)</summary>
    [ApiController]
    [Route("api/PublicHolidays")]
    [Authorize]
    public class PublicHolidaysController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PublicHolidaysController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("GetAll")]
        [Authorize(Policy = "ViewHolidays")]
        public async Task<ActionResult<List<PublicHolidayResponseDto>>> GetAll([FromQuery] int? year)
            => Ok(await _mediator.Send(new GetAllPublicHolidaysQuery(year)));

        // EndDate اختياري: يضيف يوماً لكل تاريخ في المدة (مثل عطلة العيد)
        [HttpPost("Create")]
        [Authorize(Policy = "CreateHoliday")]
        public async Task<ActionResult<List<PublicHolidayResponseDto>>> Create([FromBody] PublicHolidayRequestDto dto)
            => Ok(await _mediator.Send(new CreatePublicHolidayCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditHoliday")]
        public async Task<ActionResult<PublicHolidayResponseDto>> Update(int id, [FromBody] PublicHolidayRequestDto dto)
            => Ok(await _mediator.Send(new UpdatePublicHolidayCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteHoliday")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeletePublicHolidayCommand(id));
            return Ok(new { message = "تم حذف العطلة بنجاح" });
        }
    }
}
