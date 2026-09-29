using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Maintenance.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// مهام الصيانة: صلاحية منفصلة لكل عملية، والنطاق
    /// (صاحب المهمة / رئيس قسمه / السوبر ادمن) يُفحص داخل المعالج.
    /// </summary>
    [ApiController]
    [Route("api/MaintenanceTasks")]
    [Authorize]
    public class MaintenanceTasksController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MaintenanceTasksController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>userId اختياري: لرئيس القسم كي يعرض مهام موظف معيّن</summary>
        [HttpGet("GetAll")]
        [Authorize(Policy = "ViewMaintenanceTasks")]
        public async Task<ActionResult<PagedResultDto<MaintenanceTaskResponseDto>>> GetAll(
            [FromQuery] int? userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(await _mediator.Send(new GetMaintenanceTasksQuery(userId, page, pageSize)));

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewMaintenanceTasks")]
        public async Task<ActionResult<MaintenanceTaskResponseDto>> GetById(int id)
            => Ok(await _mediator.Send(new GetMaintenanceTaskByIdQuery(id)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateMaintenanceTask")]
        public async Task<ActionResult<MaintenanceTaskResponseDto>> Create([FromBody] SaveMaintenanceTaskDto dto)
            => Ok(await _mediator.Send(new CreateMaintenanceTaskCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditMaintenanceTask")]
        public async Task<ActionResult<MaintenanceTaskResponseDto>> Update(int id, [FromBody] SaveMaintenanceTaskDto dto)
            => Ok(await _mediator.Send(new UpdateMaintenanceTaskCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteMaintenanceTask")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteMaintenanceTaskCommand(id));
            return Ok(new { message = "تم حذف المهمة بنجاح" });
        }
    }
}
