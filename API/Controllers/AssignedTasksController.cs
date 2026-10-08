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
    /// الوصول كله يحتاج ViewTaskBoard، وبقية الفحوص بصلاحيات الدور (AssignTaskTo*, HandleUnitTasks) وحدودها داخل Features/AssignedTasks/AssignedTaskRules.
    /// </summary>
    [ApiController]
    [Route("api/AssignedTasks")]
    [Authorize(Policy = "ViewTaskBoard")]
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
        // type: Department | Office | User (الافتراضي أول نوع متاح) — parentTaskId: جهات التفويض من مهمة واردة
        public async Task<ActionResult<List<TaskTargetOptionDto>>> Targets([FromQuery] string? type = null, [FromQuery] int? parentTaskId = null)
            => Ok(await _mediator.Send(new GetTaskTargetsQuery(type, parentTaskId)));

        [HttpPost("Create")]
        public async Task<ActionResult<AssignedTaskDetailDto>> Create([FromBody] CreateAssignedTaskRequestDto dto)
            => Ok(await _mediator.Send(new CreateAssignedTaskCommand(dto)));

        [HttpPut("Update/{id}")]
        public async Task<ActionResult<AssignedTaskDetailDto>> Update(int id, [FromBody] UpdateAssignedTaskRequestDto dto)
            => Ok(await _mediator.Send(new UpdateAssignedTaskCommand(id, dto)));

        [HttpPut("Status/{id}")]
        public async Task<ActionResult<AssignedTaskCardDto>> Status(int id, [FromBody] ChangeAssignedTaskStatusRequestDto dto)
            => Ok(await _mediator.Send(new ChangeAssignedTaskStatusCommand(id, dto.Status, dto.Note)));

        [HttpPost("Comment/{id}")]
        public async Task<ActionResult<AssignedTaskDetailDto>> Comment(int id, [FromBody] AddAssignedTaskCommentRequestDto dto)
            => Ok(await _mediator.Send(new AddAssignedTaskCommentCommand(id, dto.Text)));

        // ════════ المرفقات (قرار المستخدم 2026-10-07): من يطّلع على المهمة، قبل «تم التنفيذ» ════════

        /// <summary>رفع ملف أو أكثر (files): PDF/صور/Office حتى 10MB للملف و10 ملفات للمهمة — يُرجع تفصيل المهمة</summary>
        [HttpPost("Attachments/{id}")]
        [RequestSizeLimit(35 * 1024 * 1024)]
        public async Task<ActionResult<AssignedTaskDetailDto>> Attach(int id, [FromForm] List<IFormFile>? files)
        {
            var uploads = new List<UploadedFileDto>();
            foreach (var file in files ?? [])
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                uploads.Add(new UploadedFileDto(file.FileName, stream.ToArray()));
            }
            return Ok(await _mediator.Send(new UploadTaskAttachmentsCommand(id, uploads)));
        }

        /// <summary>عرض المرفق (صورة/PDF داخل الصفحة)، أو تنزيله مع ?download=true — ملفات Office تُنزَّل دائماً</summary>
        [HttpGet("Attachment/{id}")]
        public async Task<IActionResult> Attachment(int id, [FromQuery] bool download = false)
        {
            var file = await _mediator.Send(new GetTaskAttachmentQuery(id));
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return download || !file.Previewable
                ? File(file.Data, file.ContentType, file.FileName)
                : File(file.Data, file.ContentType);
        }

        [HttpDelete("Attachment/{id}")]
        public async Task<ActionResult<AssignedTaskDetailDto>> DeleteAttachment(int id)
            => Ok(await _mediator.Send(new DeleteTaskAttachmentCommand(id)));

        // ════════ قائمة التحقق ════════
        [HttpPost("Checklist/{id}")]
        public async Task<ActionResult<AssignedTaskDetailDto>> AddChecklistItem(int id, [FromBody] AddChecklistItemRequestDto dto)
            => Ok(await _mediator.Send(new AddChecklistItemCommand(id, dto.Text)));

        [HttpPut("ChecklistItem/{id}")]
        public async Task<ActionResult<AssignedTaskDetailDto>> UpdateChecklistItem(int id, [FromBody] UpdateChecklistItemRequestDto dto)
            => Ok(await _mediator.Send(new UpdateChecklistItemCommand(id, dto.IsDone, dto.Text)));

        [HttpDelete("ChecklistItem/{id}")]
        public async Task<ActionResult<AssignedTaskDetailDto>> DeleteChecklistItem(int id)
            => Ok(await _mediator.Send(new DeleteChecklistItemCommand(id)));

        // ════════ «أتولّى هذه المهمة» ════════
        [HttpPut("Claim/{id}")]
        public async Task<ActionResult<AssignedTaskDetailDto>> Claim(int id, [FromBody] ClaimAssignedTaskRequestDto dto)
            => Ok(await _mediator.Send(new ClaimAssignedTaskCommand(id, dto.Claim)));

        [HttpDelete("Delete/{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteAssignedTaskCommand(id));
            return Ok(new { message = "تم حذف المهمة" });
        }
    }
}
