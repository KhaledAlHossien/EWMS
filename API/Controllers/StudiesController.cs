using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Studies;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// الدراسات والمكاتب الفنية. الصلاحية تفتح العملية، وحدّ الأوامر ثابت في StudyRules (حدّ رؤية المشروع نفسه) ويُفحص داخل المعالج.
    /// المراجع غير المُعدّ، والنظام ينبّه ولا يقرر. أمر الدراسة لا يُحذف: يُلغى.
    /// </summary>
    [ApiController]
    [Route("api/Studies")]
    [Authorize]
    public class StudiesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public StudiesController(IMediator mediator) => _mediator = mediator;

        /// <summary>بحث مرقّم (الأحدث أولاً) ضمن مشاريعي. mine=true: ما أُسند إليّ فيه مخرج. status: 1 قيد الإعداد … 5 ملغاة</summary>
        [HttpGet("Search")]
        [Authorize(Policy = "AnyStudyView")]
        public async Task<ActionResult<PagedResultDto<StudyListItemDto>>> Search(
            [FromQuery] int? projectId, [FromQuery] int? branchId, [FromQuery] int? status, [FromQuery] string? search,
            [FromQuery] bool overdueOnly = false, [FromQuery] bool mine = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(await _mediator.Send(new SearchStudiesQuery(projectId, branchId, status, search, overdueOnly, mine, page, pageSize)));

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "AnyStudyView")]
        public async Task<ActionResult<StudyDetailDto>> Get(int id) => Ok(await _mediator.Send(new GetStudyQuery(id)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateStudyOrder")]
        public async Task<ActionResult<StudyDetailDto>> Create([FromBody] StudyOrderRequestDto dto) => Ok(await _mediator.Send(new CreateStudyOrderCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditStudyOrder")]
        public async Task<ActionResult<StudyDetailDto>> Update(int id, [FromBody] StudyOrderRequestDto dto) => Ok(await _mediator.Send(new UpdateStudyOrderCommand(id, dto)));

        [HttpPost("Cancel/{id}")]
        [Authorize(Policy = "EditStudyOrder")]
        public async Task<ActionResult<StudyDetailDto>> Cancel(int id, [FromBody] StudyStatusRequestDto dto) => Ok(await _mediator.Send(new CancelStudyOrderCommand(id, dto.RowVersion)));

        /// <summary>جولة جديدة بعد ملاحظات المركزية</summary>
        [HttpPost("Reopen/{id}")]
        [Authorize(Policy = "EditStudyOrder")]
        public async Task<ActionResult<StudyDetailDto>> Reopen(int id, [FromBody] StudyStatusRequestDto dto) => Ok(await _mediator.Send(new ReopenStudyOrderCommand(id, dto.RowVersion)));

        // ───────── المخرجات ─────────

        [HttpPost("Deliverables/{studyId}")]
        [Authorize(Policy = "EditStudyOrder")]
        public async Task<ActionResult<StudyDetailDto>> AddDeliverable(int studyId, [FromBody] StudyDeliverableRequestDto dto)
            => Ok(await _mediator.Send(new AddStudyDeliverableCommand(studyId, dto)));

        [HttpPut("Deliverable/{id}")]
        [Authorize(Policy = "EditStudyOrder")]
        public async Task<ActionResult<StudyDetailDto>> UpdateDeliverable(int id, [FromBody] StudyDeliverableRequestDto dto)
            => Ok(await _mediator.Send(new UpdateStudyDeliverableCommand(id, dto)));

        [HttpDelete("Deliverable/{id}")]
        [Authorize(Policy = "EditStudyOrder")]
        public async Task<ActionResult<StudyDetailDto>> RemoveDeliverable(int id) => Ok(await _mediator.Send(new RemoveStudyDeliverableCommand(id)));

        /// <summary>ربط وثيقة رفعها المُعِدّ في إضبارة المشروع بالمخرج (مرة واحدة؛ الإصدارات التالية تُضاف إلى الوثيقة)</summary>
        [HttpPut("Deliverable/{id}/Document")]
        [Authorize(Policy = "PrepareStudyDeliverable")]
        public async Task<ActionResult<StudyDetailDto>> AttachDocument(int id, [FromBody] AttachDeliverableDocumentRequestDto dto)
            => Ok(await _mediator.Send(new AttachDeliverableDocumentCommand(id, dto)));

        /// <summary>رفع المخرج للمراجعة: يُثبَّت أحدث إصدار ويتجمّد المخرج</summary>
        [HttpPost("Deliverable/{id}/Submit")]
        [Authorize(Policy = "PrepareStudyDeliverable")]
        public async Task<ActionResult<StudyDetailDto>> Submit(int id, [FromBody] SubmitDeliverableRequestDto dto)
            => Ok(await _mediator.Send(new SubmitDeliverableCommand(id, dto.RowVersion)));

        /// <summary>اعتماد المخرج أو إعادته بسبب (غير من أعدّه أو رفع ملفه)</summary>
        [HttpPost("Deliverable/{id}/Review")]
        [Authorize(Policy = "ReviewStudyDeliverable")]
        public async Task<ActionResult<StudyDetailDto>> Review(int id, [FromBody] ReviewDeliverableRequestDto dto)
            => Ok(await _mediator.Send(new ReviewDeliverableCommand(id, dto)));

        /// <summary>إعادة مخرج معتمد إلى الإعداد لجولة جديدة (بسبب مكتوب)</summary>
        [HttpPost("Deliverable/{id}/SendBack")]
        [Authorize(Policy = "EditStudyOrder")]
        public async Task<ActionResult<StudyDetailDto>> SendBack(int id, [FromBody] SendBackDeliverableRequestDto dto)
            => Ok(await _mediator.Send(new SendBackDeliverableCommand(id, dto)));

        // ───────── المركزية ─────────

        /// <summary>تسجيل إرسال الدراسة إلى المركزية (بعد اعتماد كل مخرجاتها)</summary>
        [HttpPost("Send/{studyId}")]
        [Authorize(Policy = "RecordStudySubmission")]
        public async Task<ActionResult<StudyDetailDto>> Send(int studyId, [FromBody] SendStudyRequestDto dto)
            => Ok(await _mediator.Send(new SendStudyCommand(studyId, dto)));

        /// <summary>تسجيل قرار المركزية على الجولة المعلّقة: 2 اعتماد، 3 ملاحظات (تشترط نصها)</summary>
        [HttpPost("Decide/{submissionId}")]
        [Authorize(Policy = "RecordStudySubmission")]
        public async Task<ActionResult<StudyDetailDto>> Decide(int submissionId, [FromBody] DecideStudyRequestDto dto)
            => Ok(await _mediator.Send(new DecideStudyCommand(submissionId, dto)));
    }

    /// <summary>أنواع المخرجات الفنية: يعرّفها كل فرع بأسمائه (إعدادات). حدّها أنواع فرعه، ولنظرة المؤسسة أي فرع</summary>
    [ApiController]
    [Route("api/DeliverableTypes")]
    [Authorize]
    public class DeliverableTypesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public DeliverableTypesController(IMediator mediator) => _mediator = mediator;

        [HttpGet("GetAll")]
        [Authorize(Policy = "AnyDeliverableTypeView")]
        public async Task<ActionResult<List<DeliverableTypeResponseDto>>> GetAll([FromQuery] int? branchId, [FromQuery] bool includeInactive = false)
            => Ok(await _mediator.Send(new GetDeliverableTypesQuery(branchId, includeInactive)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateDeliverableType")]
        public async Task<ActionResult<DeliverableTypeResponseDto>> Create([FromBody] DeliverableTypeRequestDto dto) => Ok(await _mediator.Send(new CreateDeliverableTypeCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditDeliverableType")]
        public async Task<ActionResult<DeliverableTypeResponseDto>> Update(int id, [FromBody] DeliverableTypeRequestDto dto) => Ok(await _mediator.Send(new UpdateDeliverableTypeCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteDeliverableType")]
        public async Task<ActionResult<bool>> Delete(int id) => Ok(await _mediator.Send(new DeleteDeliverableTypeCommand(id)));
    }
}
