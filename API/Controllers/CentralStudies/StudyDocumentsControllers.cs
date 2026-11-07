using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.CentralStudies.Assignments;
using Application.Features.CentralStudies.Requests;
using Application.Features.CentralStudies.Responses;
using Application.Features.CentralStudies.StartOrders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    // سلسلة ما قبل المشروع: الطلب ← التكليف (IPA) ← الرد ← أمر البدء (PIO). كل مستند:
    // GetAll (بحث مقسّم صفحات) / Get / Create / Update / Delete (المسودة فقط) / Submit (إرسال للاعتماد) / Cancel (مع السبب).
    // حدّ كل الصلاحيات: مستندات فرعه (والمؤسسة كلها لنظرة المؤسسة) — يطبّقه الـ handler.

    /// <summary>طلبات تجهيز الدراسة — Submit يسجّل الطلب نهائياً (معتمد مباشرة، بلا تواقيع)</summary>
    [ApiController]
    [Route("api/StudyRequests")]
    [Authorize]
    public class StudyRequestsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public StudyRequestsController(IMediator mediator) => _mediator = mediator;

        /// <summary>بحث: status، search (الرقم أو الجهة)، branchId (لنظرة المؤسسة)، from، to، page، pageSize</summary>
        [HttpGet("GetAll")]
        [Authorize(Policy = "ViewStudyRequests")]
        public async Task<ActionResult<PagedResultDto<StudyRequestDto>>> GetAll([FromQuery] StudyDocumentFilterDto filter)
            => Ok(await _mediator.Send(new SearchStudyRequestsQuery(filter)));

        /// <summary>الطلب مع سلسلة الدراسة (Chain) وأعلام الإجراءات</summary>
        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewStudyRequests")]
        public async Task<ActionResult<StudyRequestDto>> Get(int id) => Ok(await _mediator.Send(new GetStudyRequestQuery(id)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateStudyRequest")]
        public async Task<ActionResult<StudyRequestDto>> Create([FromBody] StudyRequestFormDto dto)
            => Ok(await _mediator.Send(new CreateStudyRequestCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditStudyRequest")]
        public async Task<ActionResult<StudyRequestDto>> Update(int id, [FromBody] StudyRequestFormDto dto)
            => Ok(await _mediator.Send(new UpdateStudyRequestCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteStudyRequest")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteStudyRequestCommand(id));
            return Ok(new { message = "تم حذف الطلب" });
        }

        [HttpPut("Submit/{id}")]
        [Authorize(Policy = "EditStudyRequest")]
        public async Task<ActionResult<StudyRequestDto>> Submit(int id) => Ok(await _mediator.Send(new SubmitStudyRequestCommand(id)));

        [HttpPut("Cancel/{id}")]
        [Authorize(Policy = "CancelStudyDocuments")]
        public async Task<ActionResult<StudyRequestDto>> Cancel(int id, [FromBody] StudyCancelDto dto)
            => Ok(await _mediator.Send(new CancelStudyRequestCommand(id, dto.Reason)));
    }

    /// <summary>التكليفات الداخلية (IPA) — يعتمدها رئيس الفرع</summary>
    [ApiController]
    [Route("api/StudyAssignments")]
    [Authorize]
    public class StudyAssignmentsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public StudyAssignmentsController(IMediator mediator) => _mediator = mediator;

        [HttpGet("GetAll")]
        [Authorize(Policy = "ViewStudyAssignments")]
        public async Task<ActionResult<PagedResultDto<StudyAssignmentDto>>> GetAll([FromQuery] StudyDocumentFilterDto filter)
            => Ok(await _mediator.Send(new SearchStudyAssignmentsQuery(filter)));

        /// <summary>التكليف مع السلسلة وسطور التوقيع</summary>
        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewStudyAssignments")]
        public async Task<ActionResult<StudyAssignmentDto>> Get(int id) => Ok(await _mediator.Send(new GetStudyAssignmentQuery(id)));

        /// <summary>لطلب معتمد بلا تكليف (RequestId في النموذج)</summary>
        [HttpPost("Create")]
        [Authorize(Policy = "CreateStudyAssignment")]
        public async Task<ActionResult<StudyAssignmentDto>> Create([FromBody] StudyAssignmentFormDto dto)
            => Ok(await _mediator.Send(new CreateStudyAssignmentCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditStudyAssignment")]
        public async Task<ActionResult<StudyAssignmentDto>> Update(int id, [FromBody] StudyAssignmentFormDto dto)
            => Ok(await _mediator.Send(new UpdateStudyAssignmentCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteStudyAssignment")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteStudyAssignmentCommand(id));
            return Ok(new { message = "تم حذف التكليف" });
        }

        [HttpPut("Submit/{id}")]
        [Authorize(Policy = "EditStudyAssignment")]
        public async Task<ActionResult<StudyAssignmentDto>> Submit(int id) => Ok(await _mediator.Send(new SubmitStudyAssignmentCommand(id)));

        [HttpPut("Cancel/{id}")]
        [Authorize(Policy = "CancelStudyDocuments")]
        public async Task<ActionResult<StudyAssignmentDto>> Cancel(int id, [FromBody] StudyCancelDto dto)
            => Ok(await _mediator.Send(new CancelStudyAssignmentCommand(id, dto.Reason)));
    }

    /// <summary>إشعارات الرد على التكليف — يوقّعها القسم المتابع ثم التخطيط وإدارة المشاريع</summary>
    [ApiController]
    [Route("api/StudyResponses")]
    [Authorize]
    public class StudyResponsesController : ControllerBase
    {
        private readonly IMediator _mediator;
        public StudyResponsesController(IMediator mediator) => _mediator = mediator;

        [HttpGet("GetAll")]
        [Authorize(Policy = "ViewStudyResponses")]
        public async Task<ActionResult<PagedResultDto<StudyResponseDto>>> GetAll([FromQuery] StudyDocumentFilterDto filter)
            => Ok(await _mediator.Send(new SearchStudyResponsesQuery(filter)));

        /// <summary>الرد مع أجزائه (المباني، بنود التكلفة وإجماليها، الكادر، الموارد) والسلسلة والتواقيع</summary>
        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewStudyResponses")]
        public async Task<ActionResult<StudyResponseDto>> Get(int id) => Ok(await _mediator.Send(new GetStudyResponseQuery(id)));

        /// <summary>لتكليف معتمد بلا رد (AssignmentId في النموذج) — الأجزاء ترسل كاملة</summary>
        [HttpPost("Create")]
        [Authorize(Policy = "CreateStudyResponse")]
        public async Task<ActionResult<StudyResponseDto>> Create([FromBody] StudyResponseFormDto dto)
            => Ok(await _mediator.Send(new CreateStudyResponseCommand(dto)));

        /// <summary>جزء القسم المتابع (مسودة أو مرفوض) — المباني والتكلفة وجدولها تُرسل كاملة وتُستبدل</summary>
        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditStudyResponse")]
        public async Task<ActionResult<StudyResponseDto>> Update(int id, [FromBody] StudyResponseFormDto dto)
            => Ok(await _mediator.Send(new UpdateStudyResponseCommand(id, dto)));

        /// <summary>جزء التخطيط وإدارة المشاريع — بعد توقيع القسم المتابع وقبل توقيع التخطيط (الأجزاء تُرسل كاملة)</summary>
        [HttpPut("UpdatePlanning/{id}")]
        [Authorize(Policy = "EditStudyResponsePlanning")]
        public async Task<ActionResult<StudyResponseDto>> UpdatePlanning(int id, [FromBody] StudyResponsePlanningFormDto dto)
            => Ok(await _mediator.Send(new UpdateStudyResponsePlanningCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteStudyResponse")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteStudyResponseCommand(id));
            return Ok(new { message = "تم حذف إشعار الرد" });
        }

        [HttpPut("Submit/{id}")]
        [Authorize(Policy = "EditStudyResponse")]
        public async Task<ActionResult<StudyResponseDto>> Submit(int id) => Ok(await _mediator.Send(new SubmitStudyResponseCommand(id)));

        [HttpPut("Cancel/{id}")]
        [Authorize(Policy = "CancelStudyDocuments")]
        public async Task<ActionResult<StudyResponseDto>> Cancel(int id, [FromBody] StudyCancelDto dto)
            => Ok(await _mediator.Send(new CancelStudyResponseCommand(id, dto.Reason)));
    }

    /// <summary>أوامر بدء الدراسة (PIO) — يوقّعها رئيس القسم المختص ثم رئيس الفرع</summary>
    [ApiController]
    [Route("api/StudyStartOrders")]
    [Authorize]
    public class StudyStartOrdersController : ControllerBase
    {
        private readonly IMediator _mediator;
        public StudyStartOrdersController(IMediator mediator) => _mediator = mediator;

        [HttpGet("GetAll")]
        [Authorize(Policy = "ViewStudyStartOrders")]
        public async Task<ActionResult<PagedResultDto<StudyStartOrderDto>>> GetAll([FromQuery] StudyDocumentFilterDto filter)
            => Ok(await _mediator.Send(new SearchStudyStartOrdersQuery(filter)));

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewStudyStartOrders")]
        public async Task<ActionResult<StudyStartOrderDto>> Get(int id) => Ok(await _mediator.Send(new GetStudyStartOrderQuery(id)));

        /// <summary>لرد معتمد توصيته ليست «غير متاح» (ResponseId في النموذج)</summary>
        [HttpPost("Create")]
        [Authorize(Policy = "CreateStudyStartOrder")]
        public async Task<ActionResult<StudyStartOrderDto>> Create([FromBody] StudyStartOrderFormDto dto)
            => Ok(await _mediator.Send(new CreateStudyStartOrderCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditStudyStartOrder")]
        public async Task<ActionResult<StudyStartOrderDto>> Update(int id, [FromBody] StudyStartOrderFormDto dto)
            => Ok(await _mediator.Send(new UpdateStudyStartOrderCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteStudyStartOrder")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteStudyStartOrderCommand(id));
            return Ok(new { message = "تم حذف أمر البدء" });
        }

        [HttpPut("Submit/{id}")]
        [Authorize(Policy = "EditStudyStartOrder")]
        public async Task<ActionResult<StudyStartOrderDto>> Submit(int id) => Ok(await _mediator.Send(new SubmitStudyStartOrderCommand(id)));

        [HttpPut("Cancel/{id}")]
        [Authorize(Policy = "CancelStudyDocuments")]
        public async Task<ActionResult<StudyStartOrderDto>> Cancel(int id, [FromBody] StudyCancelDto dto)
            => Ok(await _mediator.Send(new CancelStudyStartOrderCommand(id, dto.Reason)));
    }
}
