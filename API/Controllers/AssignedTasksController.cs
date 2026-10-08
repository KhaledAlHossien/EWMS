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

        // mode: incoming | outgoing | scope — doneDays: عمر المنجزة المعروضة (الافتراضي 30)
        [HttpGet("Board")]
        public async Task<ActionResult<TaskBoardDto>> Board([FromQuery] string mode = "incoming", [FromQuery] int? doneDays = null)
            => Ok(await _mediator.Send(new GetTaskBoardQuery(mode, doneDays)));

        /// <summary>تصدير مهام العرض الحالي إلى Excel (نفس فلاتر الواجهة)</summary>
        [HttpGet("Export")]
        public async Task<IActionResult> Export([FromQuery] AssignedTaskExportFilterDto filter)
            => File(await _mediator.Send(new ExportAssignedTasksQuery(filter)),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"tasks-{DateTime.Now:yyyy-MM-dd}.xlsx");

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

        // ════════ القوالب والمهام الدورية (لمن يملك صلاحية إسناد؛ خاصة بصاحبها) ════════
        [HttpGet("Templates")]
        public async Task<ActionResult<List<TaskTemplateDto>>> Templates() => Ok(await _mediator.Send(new GetTaskTemplatesQuery()));

        [HttpPost("Templates")]
        public async Task<ActionResult<TaskTemplateDto>> CreateTemplate([FromBody] SaveTaskTemplateRequestDto dto)
            => Ok(await _mediator.Send(new SaveTaskTemplateCommand(null, dto)));

        [HttpPut("Templates/{id}")]
        public async Task<ActionResult<TaskTemplateDto>> UpdateTemplate(int id, [FromBody] SaveTaskTemplateRequestDto dto)
            => Ok(await _mediator.Send(new SaveTaskTemplateCommand(id, dto)));

        [HttpDelete("Templates/{id}")]
        public async Task<ActionResult> DeleteTemplate(int id)
        {
            await _mediator.Send(new DeleteTaskTemplateCommand(id));
            return Ok(new { message = "تم حذف القالب" });
        }

        [HttpGet("Recurrences")]
        public async Task<ActionResult<List<TaskRecurrenceDto>>> Recurrences() => Ok(await _mediator.Send(new GetTaskRecurrencesQuery()));

        [HttpPost("Recurrences")]
        public async Task<ActionResult<TaskRecurrenceDto>> CreateRecurrence([FromBody] SaveTaskRecurrenceRequestDto dto)
            => Ok(await _mediator.Send(new SaveTaskRecurrenceCommand(null, dto)));

        [HttpPut("Recurrences/{id}")]
        public async Task<ActionResult<TaskRecurrenceDto>> UpdateRecurrence(int id, [FromBody] SaveTaskRecurrenceRequestDto dto)
            => Ok(await _mediator.Send(new SaveTaskRecurrenceCommand(id, dto)));

        [HttpPut("Recurrences/{id}/Active")]
        public async Task<ActionResult<TaskRecurrenceDto>> SetRecurrenceActive(int id, [FromBody] SetRecurrenceActiveRequestDto dto)
            => Ok(await _mediator.Send(new SetTaskRecurrenceActiveCommand(id, dto.IsActive)));

        /// <summary>ينشئ مهمة من التكرار الآن دون تغيير موعده التالي</summary>
        [HttpPost("Recurrences/{id}/RunNow")]
        public async Task<ActionResult<AssignedTaskDetailDto>> RunRecurrenceNow(int id)
            => Ok(await _mediator.Send(new RunTaskRecurrenceNowCommand(id)));

        [HttpDelete("Recurrences/{id}")]
        public async Task<ActionResult> DeleteRecurrence(int id)
        {
            await _mediator.Send(new DeleteTaskRecurrenceCommand(id));
            return Ok(new { message = "تم حذف المهمة الدورية" });
        }

        // ════════ ربط المهمة بسجل في نظام آخر ════════
        [HttpGet("Links/{id}")]
        public async Task<ActionResult<List<TaskLinkDto>>> Links(int id) => Ok(await _mediator.Send(new GetTaskLinksQuery(id)));

        [HttpPost("Links/{id}")]
        public async Task<ActionResult<List<TaskLinkDto>>> AddLink(int id, [FromBody] AddTaskLinkRequestDto dto)
            => Ok(await _mediator.Send(new AddTaskLinkCommand(id, dto)));

        [HttpDelete("Link/{linkId}")]
        public async Task<ActionResult<List<TaskLinkDto>>> RemoveLink(int linkId) => Ok(await _mediator.Send(new RemoveTaskLinkCommand(linkId)));

        /// <summary>المهام المرتبطة بسجل (entityType: MaintenanceRequest | Vacation | Site) — لمن يحق له عرض السجل نفسه</summary>
        [HttpGet("ByLink")]
        public async Task<ActionResult<List<AssignedTaskCardDto>>> ByLink([FromQuery] string entityType, [FromQuery] int entityId)
            => Ok(await _mediator.Send(new GetTasksByLinkQuery(entityType, entityId)));

        // ════════ الإحصائيات (ViewTaskStats، ضمن مهام نطاقي) ════════
        [HttpGet("Stats")]
        [Authorize(Policy = "ViewTaskStats")]
        public async Task<ActionResult<TaskStatsDto>> Stats([FromQuery] DateTime? from, [FromQuery] DateTime? to)
            => Ok(await _mediator.Send(new GetTaskStatsQuery(from, to)));

        [HttpDelete("Delete/{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteAssignedTaskCommand(id));
            return Ok(new { message = "تم حذف المهمة" });
        }
    }
}
