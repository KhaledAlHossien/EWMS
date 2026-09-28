using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.WorkTasks.Commands.Create;
using Application.Features.WorkTasks.Commands.Delete;
using Application.Features.WorkTasks.Commands.Update;
using Application.Features.WorkTasks.Queries.GetAll;
using Application.Features.WorkTasks.Queries.GetById;
using Application.Features.WorkTasks.Queries.GetMy;
using Application.Features.WorkTasks.Queries.GetView;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>مهام العمل الدورية لكل فرع وإسنادها للموظفين</summary>
    [ApiController]
    [Route("api/WorkTasks")]
    [Authorize]
    public class WorkTasksController : ControllerBase
    {
        private readonly IMediator _mediator;

        public WorkTasksController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // ========== إدارة (SuperAdmin / من يملك ManageWorkTasks) ==========
        [HttpGet("GetAll")]
        [Authorize(Policy = "ManageWorkTasks")]
        public async Task<ActionResult<List<WorkTaskResponseDto>>> GetAll([FromQuery] int? branchId)
            => Ok(await _mediator.Send(new GetAllWorkTasksQuery(branchId)));

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ManageWorkTasks")]
        public async Task<ActionResult<WorkTaskResponseDto>> GetById(int id)
            => Ok(await _mediator.Send(new GetWorkTaskByIdQuery(id)));

        [HttpPost("Create")]
        [Authorize(Policy = "ManageWorkTasks")]
        public async Task<ActionResult<WorkTaskResponseDto>> Create([FromBody] CreateWorkTaskRequestDto dto)
            => Ok(await _mediator.Send(new CreateWorkTaskCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "ManageWorkTasks")]
        public async Task<ActionResult<WorkTaskResponseDto>> Update(int id, [FromBody] UpdateWorkTaskRequestDto dto)
            => Ok(await _mediator.Send(new UpdateWorkTaskCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "ManageWorkTasks")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteWorkTaskCommand(id));
            return Ok(new { message = "تم حذف المهمة بنجاح" });
        }

        // ========== للموظفين ==========
        [HttpGet("My")]
        public async Task<ActionResult<List<WorkTaskCardDto>>> GetMy()
            => Ok(await _mediator.Send(new GetMyWorkTasksQuery()));

        [HttpGet("View/{id}")]
        public async Task<ActionResult<WorkTaskCardDto>> View(int id)
            => Ok(await _mediator.Send(new GetWorkTaskViewQuery(id)));
    }
}
