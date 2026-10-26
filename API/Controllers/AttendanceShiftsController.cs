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

    /// <summary>الجدولة: إسناد الورديات لجهات منطقتي أو الكل (الحد في AttendanceScheduleHandler)</summary>
    [ApiController]
    [Route("api/AttendanceSchedules")]
    [Authorize(Policy = "AnyAttendanceSchedule")]
    public class AttendanceSchedulesController : ControllerBase
    {
        private readonly IMediator _mediator;
        public AttendanceSchedulesController(IMediator mediator) => _mediator = mediator;

        [HttpGet("GetAll")]
        public async Task<ActionResult<List<AttendanceScheduleDto>>> GetAll([FromQuery] int? targetType, [FromQuery] int? targetId) =>
            Ok(await _mediator.Send(new GetAttendanceSchedulesQuery(targetType, targetId)));

        [HttpPost("Create")]
        public async Task<ActionResult<AttendanceScheduleDto>> Create([FromBody] AttendanceScheduleRequestDto dto) =>
            Ok(await _mediator.Send(new CreateAttendanceScheduleCommand(dto)));

        [HttpPut("Update/{id:int}")]
        public async Task<ActionResult<AttendanceScheduleDto>> Update(int id, [FromBody] AttendanceScheduleRequestDto dto) =>
            Ok(await _mediator.Send(new UpdateAttendanceScheduleCommand(id, dto)));

        [HttpDelete("Delete/{id:int}")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteAttendanceScheduleCommand(id));
            return Ok(new { message = "حُذف الإسناد" });
        }

        [HttpGet("Temporary")]
        public async Task<ActionResult<List<AttendanceTemporaryDto>>> Temporaries([FromQuery] int? targetType, [FromQuery] int? targetId) =>
            Ok(await _mediator.Send(new GetAttendanceTemporariesQuery(targetType, targetId)));

        [HttpPost("Temporary")]
        public async Task<ActionResult<AttendanceTemporaryDto>> CreateTemporary([FromBody] AttendanceTemporaryRequestDto dto) =>
            Ok(await _mediator.Send(new CreateAttendanceTemporaryCommand(dto)));

        [HttpDelete("Temporary/{id:int}")]
        public async Task<ActionResult> DeleteTemporary(int id)
        {
            await _mediator.Send(new DeleteAttendanceTemporaryCommand(id));
            return Ok(new { message = "حُذفت الجدولة المؤقتة" });
        }

        /// <summary>الجهات القابلة للإسناد من نوع (1 منطقة … 5 شخص) ضمن حدّي</summary>
        [HttpGet("Targets")]
        public async Task<ActionResult<List<ScheduleTargetDto>>> Targets([FromQuery] int type, [FromQuery] string? query) =>
            Ok(await _mediator.Send(new GetScheduleTargetsQuery(type, query)));

        /// <summary>الجدول الفعلي لشخص يوماً بيوم (حتى 62 يوماً)</summary>
        [HttpGet("Effective")]
        public async Task<ActionResult<AttendanceEffectiveDto>> Effective([FromQuery] int userId, [FromQuery] DateTime from, [FromQuery] DateTime to) =>
            Ok(await _mediator.Send(new GetEffectiveScheduleQuery(userId, from, to)));

        /// <summary>أشخاص مراقَبون بلا وردية في يوم (اليوم إن لم يُحدَّد)</summary>
        [HttpGet("Unscheduled")]
        public async Task<ActionResult<List<UnscheduledPersonDto>>> Unscheduled([FromQuery] DateTime? date) =>
            Ok(await _mediator.Send(new GetUnscheduledPeopleQuery(date)));
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
