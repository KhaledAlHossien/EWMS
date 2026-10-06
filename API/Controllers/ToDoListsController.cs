using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.ToDoLists;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// قوائم المهام الشخصية. الصلاحية تفتح العملية، والحدّ ثابت: قوائم المستخدم نفسه فقط
    /// (والسوبر ادمن على الكل) — يُفحص داخل المعالج.
    /// </summary>
    [ApiController]
    [Route("api/ToDoLists")]
    [Authorize]
    public class ToDoListsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ToDoListsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>قوائمي (الأحدث أولاً). ownerId للسوبر ادمن فقط لعرض قوائم مستخدم معيّن</summary>
        [HttpGet("GetAll")]
        [Authorize(Policy = "ViewToDoLists")]
        public async Task<ActionResult<List<ToDoListResponseDto>>> GetAll([FromQuery] int? ownerId)
            => Ok(await _mediator.Send(new GetAllToDoListsQuery(ownerId)));

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewToDoLists")]
        public async Task<ActionResult<ToDoListResponseDto>> GetById(int id)
            => Ok(await _mediator.Send(new GetToDoListByIdQuery(id)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateToDoList")]
        public async Task<ActionResult<ToDoListResponseDto>> Create([FromBody] ToDoListRequestDto dto)
            => Ok(await _mediator.Send(new CreateToDoListCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditToDoList")]
        public async Task<ActionResult<ToDoListResponseDto>> Update(int id, [FromBody] ToDoListRequestDto dto)
            => Ok(await _mediator.Send(new UpdateToDoListCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteToDoList")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteToDoListCommand(id));
            return Ok(new { message = "تم حذف القائمة بنجاح" });
        }
    }
}
