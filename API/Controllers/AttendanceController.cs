using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Attendance;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>مناطق الدوام: سجل واحد للمؤسسة يُدار من المركز</summary>
    [ApiController]
    [Route("api/AttendanceAreas")]
    [Authorize]
    public class AttendanceAreasController : ControllerBase
    {
        private readonly IMediator _mediator;
        public AttendanceAreasController(IMediator mediator) => _mediator = mediator;

        [HttpGet("GetAll")]
        [Authorize(Policy = "AnyAttendanceAreaRead")]
        public async Task<ActionResult<List<AttendanceAreaDto>>> GetAll() =>
            Ok(await _mediator.Send(new GetAttendanceAreasQuery()));

        [HttpGet("Get/{id:int}")]
        [Authorize(Policy = "AnyAttendanceAreaRead")]
        public async Task<ActionResult<AttendanceAreaDto>> Get(int id) =>
            Ok(await _mediator.Send(new GetAttendanceAreaQuery(id)));

        [HttpGet("BranchOptions")]
        [Authorize(Policy = "AnyAttendanceAreaWrite")]
        public async Task<ActionResult<List<AreaBranchOptionDto>>> BranchOptions() =>
            Ok(await _mediator.Send(new GetAreaBranchOptionsQuery()));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateAttendanceArea")]
        public async Task<ActionResult<AttendanceAreaDto>> Create([FromBody] AttendanceAreaRequestDto dto) =>
            Ok(await _mediator.Send(new CreateAttendanceAreaCommand(dto)));

        [HttpPut("Update/{id:int}")]
        [Authorize(Policy = "EditAttendanceArea")]
        public async Task<ActionResult<AttendanceAreaDto>> Update(int id, [FromBody] AttendanceAreaRequestDto dto) =>
            Ok(await _mediator.Send(new UpdateAttendanceAreaCommand(id, dto)));

        [HttpDelete("Delete/{id:int}")]
        [Authorize(Policy = "DeleteAttendanceArea")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteAttendanceAreaCommand(id));
            return Ok(new { message = "تم حذف المنطقة" });
        }
    }

    /// <summary>أجهزة البصمة: العرض لمنطقتي أو للكل، والقبول والتعديل والإزالة من المركز (الحدود في AttendanceDeviceHandler)</summary>
    [ApiController]
    [Route("api/AttendanceDevices")]
    [Authorize]
    public class AttendanceDevicesController : ControllerBase
    {
        private readonly IMediator _mediator;
        public AttendanceDevicesController(IMediator mediator) => _mediator = mediator;

        [HttpGet("GetAll")]
        [Authorize(Policy = "AnyAttendanceDeviceView")]
        public async Task<ActionResult<List<AttendanceDeviceDto>>> GetAll([FromQuery] int? state, [FromQuery] int? areaId) =>
            Ok(await _mediator.Send(new GetAttendanceDevicesQuery(state, areaId)));

        [HttpGet("Get/{id:int}")]
        [Authorize(Policy = "AnyAttendanceDeviceView")]
        public async Task<ActionResult<AttendanceDeviceDto>> Get(int id) =>
            Ok(await _mediator.Send(new GetAttendanceDeviceQuery(id)));

        [HttpPut("Approve/{id:int}")]
        [Authorize(Policy = "ApproveAttendanceDevice")]
        public async Task<ActionResult<AttendanceDeviceDto>> Approve(int id, [FromBody] AttendanceDeviceRequestDto dto) =>
            Ok(await _mediator.Send(new ApproveAttendanceDeviceCommand(id, dto)));

        [HttpPut("Reject/{id:int}")]
        [Authorize(Policy = "ApproveAttendanceDevice")]
        public async Task<ActionResult<AttendanceDeviceDto>> Reject(int id) =>
            Ok(await _mediator.Send(new RejectAttendanceDeviceCommand(id)));

        [HttpPut("Update/{id:int}")]
        [Authorize(Policy = "EditAttendanceDevice")]
        public async Task<ActionResult<AttendanceDeviceDto>> Update(int id, [FromBody] AttendanceDeviceRequestDto dto) =>
            Ok(await _mediator.Send(new UpdateAttendanceDeviceCommand(id, dto)));

        [HttpDelete("Delete/{id:int}")]
        [Authorize(Policy = "DeleteAttendanceDevice")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new RemoveAttendanceDeviceCommand(id));
            return Ok(new { message = "أُزيل الجهاز" });
        }

        [HttpPost("Command/{id:int}")]
        [Authorize(Policy = "AnyAttendanceDeviceCommand")]
        public async Task<ActionResult<AttendanceDeviceDto>> Command(int id, [FromBody] AttendanceDeviceCommandRequestDto dto) =>
            Ok(await _mediator.Send(new SendAttendanceDeviceCommandCommand(id, dto)));

        [HttpGet("Commands/{id:int}")]
        [Authorize(Policy = "AnyAttendanceDeviceView")]
        public async Task<ActionResult<PagedResultDto<AttendanceDeviceCommandDto>>> Commands(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
            Ok(await _mediator.Send(new GetAttendanceDeviceCommandsQuery(id, page, pageSize)));

        [HttpGet("Log/{id:int}")]
        [Authorize(Policy = "AnyAttendanceDeviceView")]
        public async Task<ActionResult<List<AttendanceDeviceLogDto>>> Log(int id) =>
            Ok(await _mediator.Send(new GetAttendanceDeviceLogQuery(id)));

        [HttpGet("Users/{id:int}")]
        [Authorize(Policy = "AnyAttendanceDeviceView")]
        public async Task<ActionResult<List<AttendanceDeviceUserDto>>> Users(int id) =>
            Ok(await _mediator.Send(new GetAttendanceDeviceUsersQuery(id)));
    }

    /// <summary>البصمات الخام بحدود صلاحيات العرض، وبصماتي</summary>
    [ApiController]
    [Route("api/AttendancePunches")]
    [Authorize]
    public class AttendancePunchesController : ControllerBase
    {
        private readonly IMediator _mediator;
        public AttendancePunchesController(IMediator mediator) => _mediator = mediator;

        [HttpGet("Search")]
        [Authorize(Policy = "AnyAttendanceView")]
        public async Task<ActionResult<PagedResultDto<AttendancePunchDto>>> Search([FromQuery] AttendancePunchSearchDto dto) =>
            Ok(await _mediator.Send(new SearchAttendancePunchesQuery(dto)));

        [HttpGet("My")]
        [Authorize(Policy = "ViewMyAttendance")]
        public async Task<ActionResult<PagedResultDto<AttendancePunchDto>>> My([FromQuery] DateTime? from, [FromQuery] DateTime? to,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 100) =>
            Ok(await _mediator.Send(new GetMyPunchesQuery(from, to, page, pageSize)));

        [HttpGet("UnknownNumbers")]
        [Authorize(Policy = "AnyAttendanceView")]
        public async Task<ActionResult<List<UnknownPinDto>>> UnknownNumbers([FromQuery] int? areaId) =>
            Ok(await _mediator.Send(new GetUnknownPinsQuery(areaId)));

        [HttpGet("Photo/{id:long}")]
        [Authorize(Policy = "ViewAttendancePhotos")]
        public async Task<IActionResult> Photo(long id)
        {
            var file = await _mediator.Send(new GetPunchPhotoQuery(id));
            Response.Headers.CacheControl = "private, no-store";
            return File(file.Content, "image/jpeg");
        }
    }
}
