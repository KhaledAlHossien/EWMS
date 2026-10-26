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

    /// <summary>الأشخاص في الحضور: القائمة، بيانات الحضور، عمال المياومة، والاستيراد من Excel (الحدود في الـ handlers)</summary>
    [ApiController]
    [Route("api/AttendancePeople")]
    [Authorize]
    public class AttendancePeopleController : ControllerBase
    {
        private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        private readonly IMediator _mediator;
        public AttendancePeopleController(IMediator mediator) => _mediator = mediator;

        [HttpGet("Search")]
        [Authorize(Policy = "AnyAttendancePeople")]
        public async Task<ActionResult<PagedResultDto<AttendancePersonDto>>> Search([FromQuery] AttendancePeopleSearchDto dto) =>
            Ok(await _mediator.Send(new SearchAttendancePeopleQuery(dto)));

        [HttpGet("Get/{userId:int}")]
        [Authorize(Policy = "AnyAttendancePeople")]
        public async Task<ActionResult<AttendancePersonDto>> Get(int userId) =>
            Ok(await _mediator.Send(new GetAttendancePersonQuery(userId)));

        [HttpPut("Profile/{userId:int}")]
        [Authorize(Policy = "EditAttendanceProfile")]
        public async Task<ActionResult<AttendanceProfileSavedDto>> Profile(int userId, [FromBody] AttendanceProfileRequestDto dto) =>
            Ok(await _mediator.Send(new UpdateAttendanceProfileCommand(userId, dto)));

        [HttpDelete("Biometrics/{userId:int}")]
        [Authorize(Policy = "EditAttendanceProfile")]
        public async Task<ActionResult<AttendancePersonDto>> ClearBiometrics(int userId) =>
            Ok(await _mediator.Send(new ClearPersonBiometricsCommand(userId)));

        [HttpPost("Workers")]
        [Authorize(Policy = "CreateDailyWorker")]
        public async Task<ActionResult<AttendanceProfileSavedDto>> CreateWorker([FromBody] DailyWorkerRequestDto dto) =>
            Ok(await _mediator.Send(new CreateDailyWorkerCommand(dto)));

        [HttpPut("Worker/{userId:int}")]
        [Authorize(Policy = "EditDailyWorker")]
        public async Task<ActionResult<AttendanceProfileSavedDto>> UpdateWorker(int userId, [FromBody] DailyWorkerRequestDto dto) =>
            Ok(await _mediator.Send(new UpdateDailyWorkerCommand(userId, dto)));

        [HttpDelete("Worker/{userId:int}")]
        [Authorize(Policy = "DeleteDailyWorker")]
        public async Task<ActionResult> DeleteWorker(int userId)
        {
            await _mediator.Send(new DeleteDailyWorkerCommand(userId));
            return Ok(new { message = "حُذف العامل" });
        }

        [HttpGet("ImportTemplate")]
        [Authorize(Policy = "AnyAttendancePeopleWrite")]
        public async Task<IActionResult> ImportTemplate() =>
            File(await _mediator.Send(new GetPeopleImportTemplateQuery()), Xlsx, "attendance-people-template.xlsx");

        /// <summary>dryRun=true معاينة بلا حفظ، false يحفظ الأسطر الصالحة فقط</summary>
        [HttpPost("Import")]
        [Authorize(Policy = "AnyAttendancePeopleWrite")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<ActionResult<PeopleImportReportDto>> Import(IFormFile? file, [FromQuery] bool dryRun = true)
        {
            if (file == null || file.Length == 0) return BadRequest(new { message = "اختر ملف Excel" });
            if (file.Length > 5 * 1024 * 1024) return BadRequest(new { message = "حجم الملف يتجاوز 5MB" });
            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;
            return Ok(await _mediator.Send(new ImportAttendancePeopleCommand(stream, dryRun)));
        }
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
