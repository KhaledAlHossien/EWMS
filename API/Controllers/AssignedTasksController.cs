using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.AssignedTasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// لوحة المهام: إسناد المهام نزولاً في الهيكل ومتابعتها (لم تُنفَّذ / قيد التنفيذ / تم التنفيذ).
    /// كل الصلاحيات حسب الدور والنطاق داخل Features/AssignedTasks/AssignedTaskRules.
    /// </summary>
    [ApiController]
    [Route("api/AssignedTasks")]
    [Authorize]
    public class AssignedTasksController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AssignedTasksController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // mode: incoming | outgoing | scope
        [HttpGet("Board")]
        public async Task<ActionResult<TaskBoardDto>> Board([FromQuery] string mode = "incoming")
            => Ok(await _mediator.Send(new GetTaskBoardQuery(mode)));

        [HttpGet("Get/{id}")]
        public async Task<ActionResult<AssignedTaskDetailDto>> Get(int id)
            => Ok(await _mediator.Send(new GetAssignedTaskQuery(id)));

        [HttpGet("Targets")]
        public async Task<ActionResult<List<TaskTargetOptionDto>>> Targets()
            => Ok(await _mediator.Send(new GetTaskTargetsQuery()));

        [HttpPost("Create")]
        public async Task<ActionResult<AssignedTaskDetailDto>> Create([FromBody] CreateAssignedTaskRequestDto dto)
            => Ok(await _mediator.Send(new CreateAssignedTaskCommand(dto)));

        [HttpPut("Update/{id}")]
        public async Task<ActionResult<AssignedTaskDetailDto>> Update(int id, [FromBody] UpdateAssignedTaskRequestDto dto)
            => Ok(await _mediator.Send(new UpdateAssignedTaskCommand(id, dto)));

        [HttpPut("Status/{id}")]
        public async Task<ActionResult<AssignedTaskCardDto>> Status(int id, [FromBody] ChangeAssignedTaskStatusRequestDto dto)
            => Ok(await _mediator.Send(new ChangeAssignedTaskStatusCommand(id, dto.Status)));

        [HttpPost("Comment/{id}")]
        public async Task<ActionResult<AssignedTaskDetailDto>> Comment(int id, [FromBody] AddAssignedTaskCommentRequestDto dto)
            => Ok(await _mediator.Send(new AddAssignedTaskCommentCommand(id, dto.Text)));

        [HttpDelete("Delete/{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteAssignedTaskCommand(id));
            return Ok(new { message = "تم حذف المهمة" });
        }
    }
}
