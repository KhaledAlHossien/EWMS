using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Handover;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// الاستلام والتسليم: طلب الاستلام (أولي أو نهائي) ← لجنة ← كشف ونتيجة ← ملاحظات تُتابَع حتى تُغلق. الصلاحية تفتح العملية،
    /// وحدّ المحاضر ثابت في HandoverRules (حدّ رؤية المشروع نفسه) ويُفحص داخل المعالج. من عالج الملاحظة لا يتحقق منها، والنظام ينبّه ولا يقرر
    /// (لا يُغلق المشروع آلياً). المحضر لا يُحذف: يُلغى.
    /// </summary>
    [ApiController]
    [Route("api/Handovers")]
    [Authorize]
    public class HandoversController : ControllerBase
    {
        private readonly IMediator _mediator;

        public HandoversController(IMediator mediator) => _mediator = mediator;

        /// <summary>بحث مرقّم (الأحدث أولاً) ضمن مشاريعي. kind: 1 أولي، 2 نهائي. status: 1 طلب … 4 ملغى. openNotesOnly: فيها ملاحظات لم تُغلق</summary>
        [HttpGet("Search")]
        [Authorize(Policy = "AnyHandoverView")]
        public async Task<ActionResult<PagedResultDto<HandoverListItemDto>>> Search(
            [FromQuery] int? projectId, [FromQuery] int? branchId, [FromQuery] int? kind, [FromQuery] int? status, [FromQuery] string? search,
            [FromQuery] bool openNotesOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(await _mediator.Send(new SearchHandoversQuery(projectId, branchId, kind, status, search, openNotesOnly, page, pageSize)));

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "AnyHandoverView")]
        public async Task<ActionResult<HandoverDetailDto>> Get(int id) => Ok(await _mediator.Send(new GetHandoverQuery(id)));

        /// <summary>ما لم يكتمل قبل إغلاق المشروع (استلام نهائي، ملاحظات مفتوحة، محتجز، عقد ساري): تنبيه لا منع</summary>
        [HttpGet("ClosureWarnings/{projectId}")]
        [Authorize(Policy = "AnyHandoverView")]
        public async Task<ActionResult<ClosureWarningsDto>> ClosureWarnings(int projectId) => Ok(await _mediator.Send(new GetClosureWarningsQuery(projectId)));

        [HttpPost("Request")]
        [Authorize(Policy = "RequestHandover")]
        public async Task<ActionResult<HandoverDetailDto>> RequestHandover([FromBody] HandoverRequestDto dto) => Ok(await _mediator.Send(new RequestHandoverCommand(dto)));

        [HttpPost("Cancel/{id}")]
        [Authorize(Policy = "ManageHandover")]
        public async Task<ActionResult<HandoverDetailDto>> Cancel(int id, [FromBody] CancelHandoverRequestDto dto) => Ok(await _mediator.Send(new CancelHandoverCommand(id, dto)));

        // ───────── اللجنة والنتيجة ─────────

        /// <summary>تشكيل لجنة الاستلام (rowVersion = المحضر): ينتقل الطلب إلى «لجنة مشكَّلة»</summary>
        [HttpPost("Committee/{id}")]
        [Authorize(Policy = "ManageHandover")]
        public async Task<ActionResult<HandoverDetailDto>> FormCommittee(int id, [FromBody] CommitteeRequestDto dto)
            => Ok(await _mediator.Send(new FormHandoverCommitteeCommand(id, dto)));

        /// <summary>تعديل اللجنة قبل النتيجة (rowVersion = اللجنة): يُستبدل أعضاؤها بالقائمة المرسلة</summary>
        [HttpPut("Committee/{id}")]
        [Authorize(Policy = "ManageHandover")]
        public async Task<ActionResult<HandoverDetailDto>> UpdateCommittee(int id, [FromBody] CommitteeRequestDto dto)
            => Ok(await _mediator.Send(new UpdateHandoverCommitteeCommand(id, dto)));

        /// <summary>تسجيل الكشف والنتيجة: 1 بلا ملاحظات، 2 بملاحظات (تشترط ملاحظة مسجَّلة)</summary>
        [HttpPost("Conclude/{id}")]
        [Authorize(Policy = "ManageHandover")]
        public async Task<ActionResult<HandoverDetailDto>> Conclude(int id, [FromBody] ConcludeHandoverRequestDto dto)
            => Ok(await _mediator.Send(new ConcludeHandoverCommand(id, dto)));

        // ───────── ملاحظات اللجنة ─────────

        [HttpPost("Notes/{id}")]
        [Authorize(Policy = "FollowHandoverNotes")]
        public async Task<ActionResult<HandoverDetailDto>> AddNote(int id, [FromBody] HandoverNoteRequestDto dto) => Ok(await _mediator.Send(new AddHandoverNoteCommand(id, dto)));

        [HttpPut("Note/{noteId}")]
        [Authorize(Policy = "FollowHandoverNotes")]
        public async Task<ActionResult<HandoverDetailDto>> UpdateNote(int noteId, [FromBody] HandoverNoteRequestDto dto) => Ok(await _mediator.Send(new UpdateHandoverNoteCommand(noteId, dto)));

        [HttpDelete("Note/{noteId}")]
        [Authorize(Policy = "FollowHandoverNotes")]
        public async Task<ActionResult<HandoverDetailDto>> RemoveNote(int noteId) => Ok(await _mediator.Send(new RemoveHandoverNoteCommand(noteId)));

        /// <summary>تسجيل معالجة الملاحظة (تنتظر التحقق من غير من عالجها)</summary>
        [HttpPost("Note/{noteId}/Fix")]
        [Authorize(Policy = "FollowHandoverNotes")]
        public async Task<ActionResult<HandoverDetailDto>> FixNote(int noteId, [FromBody] FixHandoverNoteRequestDto dto) => Ok(await _mediator.Send(new FixHandoverNoteCommand(noteId, dto)));

        /// <summary>التحقق من المعالجة وإغلاق الملاحظة (غير من عالجها)</summary>
        [HttpPost("Note/{noteId}/Verify")]
        [Authorize(Policy = "FollowHandoverNotes")]
        public async Task<ActionResult<HandoverDetailDto>> VerifyNote(int noteId, [FromBody] VerifyHandoverNoteRequestDto dto) => Ok(await _mediator.Send(new VerifyHandoverNoteCommand(noteId, dto)));

        /// <summary>رفض المعالجة: تعود الملاحظة مفتوحة بسبب مكتوب</summary>
        [HttpPost("Note/{noteId}/Reject")]
        [Authorize(Policy = "FollowHandoverNotes")]
        public async Task<ActionResult<HandoverDetailDto>> RejectNoteFix(int noteId, [FromBody] VerifyHandoverNoteRequestDto dto) => Ok(await _mediator.Send(new RejectHandoverFixCommand(noteId, dto)));
    }
}
