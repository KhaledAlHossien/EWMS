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

    /// <summary>نتائج الحساب: الحضور اليومي وكشف الشخص («دوامي» للنفس)، وإعادة الحساب اليدوية</summary>
    [ApiController]
    [Route("api/AttendanceDays")]
    [Authorize]
    public class AttendanceDaysController : ControllerBase
    {
        private readonly IMediator _mediator;
        public AttendanceDaysController(IMediator mediator) => _mediator = mediator;

        [HttpGet("Daily")]
        [Authorize(Policy = "AnyAttendanceView")]
        public async Task<ActionResult<AttendanceDailyDto>> Daily([FromQuery] DateTime? date, [FromQuery] int? areaId, [FromQuery] int? branchId,
            [FromQuery] int? departmentId, [FromQuery] string? status, [FromQuery] string? query, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
            Ok(await _mediator.Send(new GetAttendanceDailyQuery(date, areaId, branchId, departmentId, status, query, page, pageSize)));

        /// <summary>كشف شهر لشخص (month = yyyy-MM، الفارغ = الحالي)</summary>
        [HttpGet("Person/{userId:int}")]
        [Authorize(Policy = "AnyAttendanceView")]
        public async Task<ActionResult<AttendanceSheetDto>> Person(int userId, [FromQuery] string? month) =>
            Ok(await _mediator.Send(new GetAttendanceSheetQuery(userId, month)));

        [HttpGet("My")]
        [Authorize(Policy = "ViewMyAttendance")]
        public async Task<ActionResult<AttendanceSheetDto>> My([FromQuery] string? month) =>
            Ok(await _mediator.Send(new GetMyAttendanceSheetQuery(month)));

        /// <summary>يضع الطلب في طابور الحساب؛ يعيد عدد الأشخاص</summary>
        [HttpPost("Recalculate")]
        [Authorize(Policy = "AnyAttendanceCorrect")]
        public async Task<ActionResult> Recalculate([FromBody] AttendanceRecalculateRequestDto dto)
        {
            var people = await _mediator.Send(new RecalculateAttendanceCommand(dto));
            return Ok(new { people, message = $"سيُعاد حساب {people} شخص خلال دقيقة" });
        }
    }

    /// <summary>التصحيح اليدوي: بصمة يدوية، تجاهل بصمة، تبرير — بحد منطقتي أو الكل، ويُلغى بسبب ولا يُحذف</summary>
    [ApiController]
    [Route("api/AttendanceCorrections")]
    [Authorize(Policy = "AnyAttendanceCorrect")]
    public class AttendanceCorrectionsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public AttendanceCorrectionsController(IMediator mediator) => _mediator = mediator;

        [HttpGet("Person/{userId:int}")]
        public async Task<ActionResult<List<AttendanceCorrectionDto>>> Person(int userId, [FromQuery] DateTime from, [FromQuery] DateTime to) =>
            Ok(await _mediator.Send(new GetAttendanceCorrectionsQuery(userId, from, to)));

        [HttpPost("Create")]
        public async Task<ActionResult<AttendanceCorrectionDto>> Create([FromBody] AttendanceCorrectionRequestDto dto) =>
            Ok(await _mediator.Send(new CreateAttendanceCorrectionCommand(dto)));

        [HttpPut("Cancel/{id:long}")]
        public async Task<ActionResult<AttendanceCorrectionDto>> Cancel(long id, [FromBody] CancelAttendanceCorrectionDto dto) =>
            Ok(await _mediator.Send(new CancelAttendanceCorrectionCommand(id, dto.Reason)));
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
