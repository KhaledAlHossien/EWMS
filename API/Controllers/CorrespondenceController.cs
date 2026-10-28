using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Diwan;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// الديوان: المعاملات الواردة والصادرة. الصلاحية تفتح العملية، وحدّ المعاملات ثابت في DiwanRules (معاملاتي ← قسمي ← فرعي ← المؤسسة،
    /// مع حامل المعالجة في الوحدة الحالية) ويُفحص داخل المعالج لكل معاملة. لا حذف للمعاملات: تُغلق بحالة ذات مرحلة «مغلقة».
    /// </summary>
    [ApiController]
    [Route("api/Correspondence")]
    [Authorize]
    public class CorrespondenceController : ControllerBase
    {
        private readonly IMediator _mediator;

        public CorrespondenceController(IMediator mediator) => _mediator = mediator;

        /// <summary>
        /// بحث مرقّم (الأحدث أولاً) ضمن ما أراه. inbox=true: ما تحمله وحدتي الآن ولم يُغلق. direction: 1 وارد، 2 صادر.
        /// search: موضوع أو جهة أو رقم الكتاب أو رقم داخلي (IN-2026-00012).
        /// </summary>
        [HttpGet("Search")]
        [Authorize(Policy = "AnyCorrespondenceView")]
        public async Task<ActionResult<PagedResultDto<CorrespondenceListItemDto>>> Search(
            [FromQuery] int? direction, [FromQuery] int? branchId, [FromQuery] int? typeId, [FromQuery] int? statusId, [FromQuery] int? stage,
            [FromQuery] int? projectId, [FromQuery] string? search, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
            [FromQuery] bool overdueOnly = false, [FromQuery] bool inbox = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(await _mediator.Send(new SearchCorrespondenceQuery(direction, branchId, typeId, statusId, stage, projectId, search, from, to,
                overdueOnly, inbox, page, pageSize)));

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "AnyCorrespondenceView")]
        public async Task<ActionResult<CorrespondenceResponseDto>> Get(int id) => Ok(await _mediator.Send(new GetCorrespondenceQuery(id)));

        /// <summary>أثر عمليات المعاملة (تسجيل، تعديل، إحالة، استلام، حالة، اعتماد، إصدار، إرفاق) الأحدث أولاً</summary>
        [HttpGet("History/{id}")]
        [Authorize(Policy = "AnyCorrespondenceView")]
        public async Task<ActionResult<List<CorrespondenceHistoryDto>>> History(int id) => Ok(await _mediator.Send(new GetCorrespondenceHistoryQuery(id)));

        /// <summary>بيانات طباعة الكتاب الصادر بعد اعتماده (بتوقيع المعتمِد المثبَّت وقت الاعتماد)</summary>
        [HttpGet("Print/{id}")]
        [Authorize(Policy = "AnyCorrespondenceView")]
        public async Task<ActionResult<CorrespondencePrintDto>> Print(int id) => Ok(await _mediator.Send(new PrintCorrespondenceQuery(id)));

        [HttpPost("RegisterIncoming")]
        [Authorize(Policy = "RegisterCorrespondence")]
        public async Task<ActionResult<CorrespondenceResponseDto>> RegisterIncoming([FromBody] RegisterIncomingRequestDto dto)
            => Ok(await _mediator.Send(new RegisterIncomingCommand(dto)));

        [HttpPost("CreateDraft")]
        [Authorize(Policy = "RegisterCorrespondence")]
        public async Task<ActionResult<CorrespondenceResponseDto>> CreateDraft([FromBody] CreateOutgoingDraftRequestDto dto)
            => Ok(await _mediator.Send(new CreateOutgoingDraftCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "AnyCorrespondenceWrite")]
        public async Task<ActionResult<CorrespondenceResponseDto>> Update(int id, [FromBody] UpdateCorrespondenceRequestDto dto)
            => Ok(await _mediator.Send(new UpdateCorrespondenceCommand(id, dto)));

        // ───────── الإحالة والاستلام والحالة ─────────

        /// <summary>أقسام فرعي ومكاتبها، لنافذة الإحالة</summary>
        [HttpGet("ReferralTargets")]
        [Authorize(Policy = "ReferCorrespondence")]
        public async Task<ActionResult<List<ReferralDepartmentDto>>> ReferralTargets([FromQuery] int? branchId)
            => Ok(await _mediator.Send(new GetReferralTargetsQuery(branchId)));

        /// <summary>من يملك معالجة المعاملات في قسم (أو مكتبه) ليُحال إليه بالاسم</summary>
        [HttpGet("ReferralUsers")]
        [Authorize(Policy = "ReferCorrespondence")]
        public async Task<ActionResult<List<ReferralUserDto>>> ReferralUsers([FromQuery] int departmentId, [FromQuery] int? officeId)
            => Ok(await _mediator.Send(new GetReferralUsersQuery(departmentId, officeId)));

        [HttpPost("Refer/{id}")]
        [Authorize(Policy = "ReferCorrespondence")]
        public async Task<ActionResult<CorrespondenceResponseDto>> Refer(int id, [FromBody] ReferCorrespondenceRequestDto dto)
            => Ok(await _mediator.Send(new ReferCorrespondenceCommand(id, dto)));

        [HttpPost("Acknowledge/{id}")]
        [Authorize(Policy = "HandleCorrespondence")]
        public async Task<ActionResult<CorrespondenceResponseDto>> Acknowledge(int id)
            => Ok(await _mediator.Send(new AcknowledgeCorrespondenceCommand(id)));

        [HttpPut("Status/{id}")]
        [Authorize(Policy = "HandleCorrespondence")]
        public async Task<ActionResult<CorrespondenceResponseDto>> Status(int id, [FromBody] ChangeCorrespondenceStatusRequestDto dto)
            => Ok(await _mediator.Send(new ChangeCorrespondenceStatusCommand(id, dto)));

        // ───────── الصادر ─────────

        [HttpPost("Submit/{id}")]
        [Authorize(Policy = "RegisterCorrespondence")]
        public async Task<ActionResult<CorrespondenceResponseDto>> Submit(int id) => Ok(await _mediator.Send(new SubmitOutgoingDraftCommand(id)));

        [HttpPost("Decide/{id}")]
        [Authorize(Policy = "ApproveOutgoingCorrespondence")]
        public async Task<ActionResult<CorrespondenceResponseDto>> Decide(int id, [FromBody] DecideOutgoingDraftRequestDto dto)
            => Ok(await _mediator.Send(new DecideOutgoingDraftCommand(id, dto)));

        [HttpPost("Issue/{id}")]
        [Authorize(Policy = "RegisterCorrespondence")]
        public async Task<ActionResult<CorrespondenceResponseDto>> Issue(int id) => Ok(await _mediator.Send(new IssueOutgoingCommand(id)));

        // ───────── المرفقات ─────────

        /// <summary>رفع ملف أو أكثر (files): PDF/صور/Office حتى 10MB للملف و20 ملفاً للمعاملة — يُرجع تفصيل المعاملة</summary>
        [HttpPost("Attachments/{id}")]
        [Authorize(Policy = "AnyCorrespondenceWrite")]
        [RequestSizeLimit(35 * 1024 * 1024)]
        public async Task<ActionResult<CorrespondenceResponseDto>> Attach(int id, [FromForm] List<IFormFile>? files)
        {
            var uploads = new List<UploadedFileDto>();
            foreach (var file in files ?? [])
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                uploads.Add(new UploadedFileDto(file.FileName, stream.ToArray()));
            }
            return Ok(await _mediator.Send(new UploadCorrespondenceAttachmentsCommand(id, uploads)));
        }

        /// <summary>عرض المرفق (صورة/PDF داخل الصفحة)، أو تنزيله مع ?download=true — ملفات Office تُنزَّل دائماً</summary>
        [HttpGet("Attachment/{id}")]
        [Authorize(Policy = "AnyCorrespondenceView")]
        public async Task<IActionResult> Attachment(int id, [FromQuery] bool download = false)
        {
            var file = await _mediator.Send(new GetCorrespondenceAttachmentQuery(id));
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return download || !file.Previewable
                ? File(file.Content, file.ContentType, file.FileName)
                : File(file.Content, file.ContentType);
        }

        [HttpDelete("Attachment/{id}")]
        [Authorize(Policy = "RegisterCorrespondence")]
        public async Task<ActionResult<CorrespondenceResponseDto>> DeleteAttachment(int id)
            => Ok(await _mediator.Send(new DeleteCorrespondenceAttachmentCommand(id)));
    }

    /// <summary>أنواع معاملات الفرع (إعدادات): القراءة لمن يسجّل أو يعالج، والكتابة بصلاحيات الأنواع والحالات</summary>
    [ApiController]
    [Route("api/CorrespondenceTypes")]
    [Authorize]
    public class CorrespondenceTypesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public CorrespondenceTypesController(IMediator mediator) => _mediator = mediator;

        [HttpGet("GetAll")]
        [Authorize(Policy = "AnyCorrespondenceLookupView")]
        public async Task<ActionResult<List<CorrespondenceTypeResponseDto>>> GetAll([FromQuery] int? branchId, [FromQuery] bool includeInactive = false)
            => Ok(await _mediator.Send(new GetCorrespondenceTypesQuery(branchId, includeInactive)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateCorrespondenceLookup")]
        public async Task<ActionResult<CorrespondenceTypeResponseDto>> Create([FromBody] CorrespondenceTypeRequestDto dto)
            => Ok(await _mediator.Send(new CreateCorrespondenceTypeCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditCorrespondenceLookup")]
        public async Task<ActionResult<CorrespondenceTypeResponseDto>> Update(int id, [FromBody] CorrespondenceTypeRequestDto dto)
            => Ok(await _mediator.Send(new UpdateCorrespondenceTypeCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteCorrespondenceLookup")]
        public async Task<IActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteCorrespondenceTypeCommand(id));
            return Ok(new { message = "تم حذف النوع" });
        }
    }

    /// <summary>حالات معاملات الفرع (إعدادات): الاسم من الفرع والمرحلة ثابتة</summary>
    [ApiController]
    [Route("api/CorrespondenceStatuses")]
    [Authorize]
    public class CorrespondenceStatusesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public CorrespondenceStatusesController(IMediator mediator) => _mediator = mediator;

        [HttpGet("GetAll")]
        [Authorize(Policy = "AnyCorrespondenceLookupView")]
        public async Task<ActionResult<List<CorrespondenceStatusResponseDto>>> GetAll([FromQuery] int? branchId, [FromQuery] bool includeInactive = false)
            => Ok(await _mediator.Send(new GetCorrespondenceStatusesQuery(branchId, includeInactive)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateCorrespondenceLookup")]
        public async Task<ActionResult<CorrespondenceStatusResponseDto>> Create([FromBody] CorrespondenceStatusRequestDto dto)
            => Ok(await _mediator.Send(new CreateCorrespondenceStatusCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditCorrespondenceLookup")]
        public async Task<ActionResult<CorrespondenceStatusResponseDto>> Update(int id, [FromBody] CorrespondenceStatusRequestDto dto)
            => Ok(await _mediator.Send(new UpdateCorrespondenceStatusCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteCorrespondenceLookup")]
        public async Task<IActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteCorrespondenceStatusCommand(id));
            return Ok(new { message = "تم حذف الحالة" });
        }
    }
}
