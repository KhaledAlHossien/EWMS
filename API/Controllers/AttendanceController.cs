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
}
