using API.Uploads;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.CentralStudies.Approvals.Commands.Decide;
using Application.Features.CentralStudies.Approvals.Queries.GetByOwner;
using Application.Features.CentralStudies.Approvals.Queries.GetPending;
using Application.Features.CentralStudies.ArchitecturalVersions.Commands.Create;
using Application.Features.CentralStudies.ArchitecturalVersions.Commands.Delete;
using Application.Features.CentralStudies.ArchitecturalVersions.Commands.Update;
using Application.Features.CentralStudies.Attachments.Commands.Delete;
using Application.Features.CentralStudies.Attachments.Commands.Upload;
using Application.Features.CentralStudies.Attachments.Queries.GetByOwner;
using Application.Features.CentralStudies.Attachments.Queries.GetFile;
using Application.Features.CentralStudies.DesignApprovals.Commands.Create;
using Application.Features.CentralStudies.DesignApprovals.Commands.Delete;
using Application.Features.CentralStudies.DesignApprovals.Commands.Update;
using Application.Features.CentralStudies.DesignApprovals.Queries.GetById;
using Application.Features.CentralStudies.Drawings.Commands.Create;
using Application.Features.CentralStudies.Drawings.Commands.Delete;
using Application.Features.CentralStudies.Drawings.Commands.Review;
using Application.Features.CentralStudies.Drawings.Commands.Update;
using Application.Features.CentralStudies.Drawings.Queries.GetById;
using Application.Features.CentralStudies.Drawings.Queries.GetByProject;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// مخططات المشاريع (كل الأنواع) ونسخ المعماري. يعدّل المخطط قسمه المُصدِر، وغير المعماري تراجعه ReviewStudyDrawing.
    /// </summary>
    [ApiController]
    [Route("api/StudyDrawings")]
    [Authorize]
    public class StudyDrawingsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public StudyDrawingsController(IMediator mediator) => _mediator = mediator;

        /// <summary>kind اختياري: 1 معماري، 2 إنشائي، 3 كهرباء وميكانيك، 4 داخلي، 5 تقني</summary>
        [HttpGet("ByProject/{projectId}")]
        [Authorize(Policy = "ViewStudyDrawings")]
        public async Task<ActionResult<List<StudyDrawingDto>>> ByProject(int projectId, [FromQuery] int? kind)
            => Ok(await _mediator.Send(new GetStudyDrawingsQuery(projectId, kind)));

        /// <summary>المخطط مع نسخه (للمعماري) واعتماداتها</summary>
        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewStudyDrawings")]
        public async Task<ActionResult<StudyDrawingDto>> Get(int id) => Ok(await _mediator.Send(new GetStudyDrawingQuery(id)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateStudyDrawing")]
        public async Task<ActionResult<StudyDrawingDto>> Create([FromBody] StudyDrawingFormDto dto)
            => Ok(await _mediator.Send(new CreateStudyDrawingCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditStudyDrawing")]
        public async Task<ActionResult<StudyDrawingDto>> Update(int id, [FromBody] StudyDrawingFormDto dto)
            => Ok(await _mediator.Send(new UpdateStudyDrawingCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteStudyDrawing")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteStudyDrawingCommand(id));
            return Ok(new { message = "تم حذف المخطط" });
        }

        /// <summary>اعتماد مخطط غير معماري بانتظار المراجعة أو رفضه (السبب مطلوب للرفض)</summary>
        [HttpPut("Review/{id}")]
        [Authorize(Policy = "ReviewStudyDrawing")]
        public async Task<ActionResult<StudyDrawingDto>> Review(int id, [FromBody] StudyDrawingReviewDto dto)
            => Ok(await _mediator.Send(new ReviewStudyDrawingCommand(id, dto)));

        // ════════ نسخ المعماري (Draft-N) ════════

        [HttpPost("Versions/{drawingId}")]
        [Authorize(Policy = "CreateArchitecturalVersion")]
        public async Task<ActionResult<StudyDrawingDto>> CreateVersion(int drawingId, [FromBody] ArchitecturalVersionFormDto dto)
            => Ok(await _mediator.Send(new CreateArchitecturalVersionCommand(drawingId, dto)));

        [HttpPut("Version/{id}")]
        [Authorize(Policy = "EditArchitecturalVersion")]
        public async Task<ActionResult<StudyDrawingDto>> UpdateVersion(int id, [FromBody] ArchitecturalVersionFormDto dto)
            => Ok(await _mediator.Send(new UpdateArchitecturalVersionCommand(id, dto)));

        [HttpDelete("Version/{id}")]
        [Authorize(Policy = "DeleteArchitecturalVersion")]
        public async Task<ActionResult<StudyDrawingDto>> DeleteVersion(int id)
            => Ok(await _mediator.Send(new DeleteArchitecturalVersionCommand(id)));
    }

    /// <summary>اعتماد المخططات الأولي (CDA) لنسخة معمارية — يوقّعه القسم المرسل ثم إدارة التصميم ثم ضبط الجودة</summary>
    [ApiController]
    [Route("api/StudyDesignApprovals")]
    [Authorize]
    public class StudyDesignApprovalsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public StudyDesignApprovalsController(IMediator mediator) => _mediator = mediator;

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewStudyDrawings")]
        public async Task<ActionResult<DesignApprovalDto>> Get(int id) => Ok(await _mediator.Send(new GetDesignApprovalQuery(id)));

        /// <summary>لنسخة قيد الإعداد: يُرقَّم CDA-yy-nnnn وتُنشأ سطور التوقيع فوراً</summary>
        [HttpPost("Create")]
        [Authorize(Policy = "CreateDesignApproval")]
        public async Task<ActionResult<DesignApprovalDto>> Create([FromBody] DesignApprovalFormDto dto)
            => Ok(await _mediator.Send(new CreateDesignApprovalCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditDesignApproval")]
        public async Task<ActionResult<DesignApprovalDto>> Update(int id, [FromBody] DesignApprovalFormDto dto)
            => Ok(await _mediator.Send(new UpdateDesignApprovalCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteDesignApproval")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteDesignApprovalCommand(id));
            return Ok(new { message = "تم حذف اعتماد المخططات" });
        }
    }

    /// <summary>
    /// التواقيع على مستندات الدراسات. ownerType: 2 تكليف، 3 رد، 4 أمر بدء، 5 مشروع (الإنجاز)، 10 اعتماد مخططات (CDA).
    /// </summary>
    [ApiController]
    [Route("api/StudyApprovals")]
    [Authorize]
    public class StudyApprovalsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public StudyApprovalsController(IMediator mediator) => _mediator = mediator;

        /// <summary>بانتظار توقيعي: السطور التي أستطيع التوقيع عليها الآن (صلاحية الصفة وحدّها والترتيب)</summary>
        [HttpGet("Pending")]
        [Authorize(Policy = "AnyStudySign")]
        public async Task<ActionResult<List<StudyApprovalQueueItemDto>>> Pending()
            => Ok(await _mediator.Send(new GetPendingStudyApprovalsQuery()));

        [HttpGet("ByOwner")]
        [Authorize(Policy = "AnyStudyView")]
        public async Task<ActionResult<List<StudyApprovalDto>>> ByOwner([FromQuery] int ownerType, [FromQuery] int ownerId)
            => Ok(await _mediator.Send(new GetStudyApprovalsQuery(ownerType, ownerId)));

        /// <summary>decision: 2 معتمد، 3 معتمد مع ملاحظات، 4 مرفوض (note مطلوبة للأخيرين)</summary>
        [HttpPut("Decide/{id}")]
        [Authorize(Policy = "AnyStudySign")]
        public async Task<ActionResult<StudyApprovalDto>> Decide(int id, [FromBody] StudyDecisionDto dto)
            => Ok(await _mediator.Send(new DecideStudyApprovalCommand(id, dto)));
    }

    /// <summary>
    /// مرفقات فرع الدراسات. ownerType: 1 طلب، 2 تكليف، 3 رد، 4 أمر بدء، 5 مشروع، 8 مخطط، 9 نسخة معمارية.
    /// العرض بصلاحية عرض المالك، والرفع والحذف بصلاحية تعديله. ملف واحد لكل طلب رفع، بلا سقف للحجم، و100 ملف للسجل.
    /// المحتوى في مجلد المرفقات على الخادم دائماً (FileStorage:Root)، والرفع والتنزيل تدفقاً.
    /// </summary>
    [ApiController]
    [Route("api/StudyAttachments")]
    [Authorize]
    public class StudyAttachmentsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public StudyAttachmentsController(IMediator mediator) => _mediator = mediator;

        [HttpGet("ByOwner")]
        [Authorize(Policy = "AnyStudyView")]
        public async Task<ActionResult<List<StudyAttachmentDto>>> ByOwner([FromQuery] int ownerType, [FromQuery] int ownerId)
            => Ok(await _mediator.Send(new GetStudyAttachmentsQuery(ownerType, ownerId)));

        /// <summary>
        /// Upload?ownerType=&amp;ownerId=&amp;attachmentTypeId= مع جسم multipart/form-data فيه حقل ملف واحد (file).
        /// المعرّفات في عنوان الطلب كي تُفحص الصلاحية قبل استقبال الملف، والملف يُقرأ تدفقاً بلا سقف للحجم.
        /// </summary>
        [HttpPost("Upload")]
        [Authorize(Policy = "AnyStudyAttachmentWrite")]
        [DisableRequestSizeLimit]
        [DisableFormValueModelBinding]
        public async Task<ActionResult<StudyAttachmentDto>> Upload([FromQuery] int ownerType, [FromQuery] int ownerId,
            [FromQuery] int attachmentTypeId, CancellationToken ct)
            => Ok(await _mediator.Send(new UploadStudyAttachmentCommand(ownerType, ownerId, attachmentTypeId,
                new MultipartUploadReader(Request)), ct));

        /// <summary>عرض الصورة/PDF داخل الصفحة، أو تنزيل الملف مع ?download=true (غير القابل للمعاينة يُنزَّل دائماً)</summary>
        [HttpGet("Get/{id}")]
        [Authorize(Policy = "AnyStudyView")]
        public async Task<IActionResult> Get(int id, [FromQuery] bool download = false)
        {
            var file = await _mediator.Send(new GetStudyAttachmentFileQuery(id));
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            // تدفق من القرص مع دعم Range: تنزيل الملفات الكبيرة يُستأنف إن انقطع
            return download || !file.Previewable
                ? File(file.Content, file.ContentType, file.FileName, enableRangeProcessing: true)
                : File(file.Content, file.ContentType, enableRangeProcessing: true);
        }

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "AnyStudyAttachmentWrite")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteStudyAttachmentCommand(id));
            return Ok(new { message = "تم حذف المرفق" });
        }
    }
}
