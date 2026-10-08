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
    /// (والسوبر ادمن على الكل) — يُفحص داخل المعالج. البنود (إضافة/تعديل/ترتيب/حذف) بصلاحية EditToDoList.
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

        // ════════ البنود (قرار المستخدم 2026-10-08): عنوان + منجز + ترتيب — كلها EditToDoList على قوائمي فقط ════════
        /// <summary>يضيف بنداً في آخر القائمة — يُرجع القائمة بعد التحديث</summary>
        [HttpPost("Items/{listId}")]
        [Authorize(Policy = "EditToDoList")]
        public async Task<ActionResult<ToDoListResponseDto>> AddItem(int listId, [FromBody] AddToDoItemRequestDto dto)
            => Ok(await _mediator.Send(new AddToDoItemCommand(listId, dto)));

        /// <summary>تعديل العنوان و/أو تعليم البند منجزاً أو لا</summary>
        [HttpPut("Item/{itemId}")]
        [Authorize(Policy = "EditToDoList")]
        public async Task<ActionResult<ToDoListResponseDto>> UpdateItem(int itemId, [FromBody] UpdateToDoItemRequestDto dto)
            => Ok(await _mediator.Send(new UpdateToDoItemCommand(itemId, dto)));

        [HttpDelete("Item/{itemId}")]
        [Authorize(Policy = "EditToDoList")]
        public async Task<ActionResult<ToDoListResponseDto>> DeleteItem(int itemId)
            => Ok(await _mediator.Send(new DeleteToDoItemCommand(itemId)));

        /// <summary>الترتيب الجديد: كل أرقام بنود القائمة مرتبة كما تريد</summary>
        [HttpPut("Reorder/{listId}")]
        [Authorize(Policy = "EditToDoList")]
        public async Task<ActionResult<ToDoListResponseDto>> Reorder(int listId, [FromBody] ReorderToDoItemsRequestDto dto)
            => Ok(await _mediator.Send(new ReorderToDoItemsCommand(listId, dto.ItemIds)));

        /// <summary>حذف كل البنود المنجزة من القائمة</summary>
        [HttpDelete("ClearDone/{listId}")]
        [Authorize(Policy = "EditToDoList")]
        public async Task<ActionResult<ToDoListResponseDto>> ClearDone(int listId)
            => Ok(await _mediator.Send(new ClearDoneToDoItemsCommand(listId)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteToDoList")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteToDoListCommand(id));
            return Ok(new { message = "تم حذف القائمة بنجاح" });
        }
    }
}
