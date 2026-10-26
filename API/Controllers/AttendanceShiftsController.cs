using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Attendance.Shifts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>الجداول الزمنية: مكتبة مشتركة للمؤسسة (يقرؤها من يديرها ومن يجدول)</summary>
    [ApiController]
    [Route("api/AttendanceTimetables")]
    [Authorize]
    public class AttendanceTimetablesController : ControllerBase
    {
        private readonly IMediator _mediator;
        public AttendanceTimetablesController(IMediator mediator) => _mediator = mediator;

        [HttpGet("GetAll")]
        [Authorize(Policy = "AnyAttendanceShiftRead")]
        public async Task<ActionResult<List<AttendanceTimetableDto>>> GetAll() =>
            Ok(await _mediator.Send(new GetAttendanceTimetablesQuery()));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateAttendanceShift")]
        public async Task<ActionResult<AttendanceTimetableDto>> Create([FromBody] AttendanceTimetableRequestDto dto) =>
            Ok(await _mediator.Send(new CreateAttendanceTimetableCommand(dto)));

        [HttpPut("Update/{id:int}")]
        [Authorize(Policy = "EditAttendanceShift")]
        public async Task<ActionResult<AttendanceTimetableDto>> Update(int id, [FromBody] AttendanceTimetableRequestDto dto) =>
            Ok(await _mediator.Send(new UpdateAttendanceTimetableCommand(id, dto)));

        [HttpDelete("Delete/{id:int}")]
        [Authorize(Policy = "DeleteAttendanceShift")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteAttendanceTimetableCommand(id));
            return Ok(new { message = "حُذف الجدول الزمني" });
        }
    }

    /// <summary>الورديات: دورة أيام لكل يوم فيها جدول أو أكثر</summary>
    [ApiController]
    [Route("api/AttendanceShifts")]
    [Authorize]
    public class AttendanceShiftsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public AttendanceShiftsController(IMediator mediator) => _mediator = mediator;

        [HttpGet("GetAll")]
        [Authorize(Policy = "AnyAttendanceShiftRead")]
        public async Task<ActionResult<List<AttendanceShiftDto>>> GetAll() =>
            Ok(await _mediator.Send(new GetAttendanceShiftsQuery()));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateAttendanceShift")]
        public async Task<ActionResult<AttendanceShiftDto>> Create([FromBody] AttendanceShiftRequestDto dto) =>
            Ok(await _mediator.Send(new CreateAttendanceShiftCommand(dto)));

        [HttpPut("Update/{id:int}")]
        [Authorize(Policy = "EditAttendanceShift")]
        public async Task<ActionResult<AttendanceShiftDto>> Update(int id, [FromBody] AttendanceShiftRequestDto dto) =>
            Ok(await _mediator.Send(new UpdateAttendanceShiftCommand(id, dto)));

        [HttpDelete("Delete/{id:int}")]
        [Authorize(Policy = "DeleteAttendanceShift")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteAttendanceShiftCommand(id));
            return Ok(new { message = "حُذفت الوردية" });
        }
    }
}
