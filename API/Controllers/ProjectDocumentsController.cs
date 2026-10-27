using Application.DTOs.Response;
using Application.Features.Projects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// وثائق إضبارة المشروع (الرفع في api/ProjectUploads). الصلاحية تفتح العملية، وحدّ الوثيقة هو حدّ رؤية مشروعها.
    /// </summary>
    [ApiController]
    [Route("api/ProjectDocuments")]
    [Authorize]
    public class ProjectDocumentsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ProjectDocumentsController(IMediator mediator) => _mediator = mediator;

        /// <summary>
        /// وثائق المشاريع التي أراها، الأحدث أولاً. projectId لإضبارة مشروع واحد، kind نوع الوثيقة، search عنوان أو اسم ملف أو اسم/رقم مشروع،
        /// from/to تاريخ رفع أي إصدار.
        /// </summary>
        [HttpGet("Search")]
        [Authorize(Policy = "ViewProjectDocuments")]
        public async Task<ActionResult<PagedResultDto<ProjectDocumentResponseDto>>> Search(
            [FromQuery] int? projectId, [FromQuery] int? kind, [FromQuery] string? search,
            [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(await _mediator.Send(new SearchProjectDocumentsQuery(projectId, kind, search, from, to, page, pageSize)));

        /// <summary>وثيقة بكل إصداراتها</summary>
        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewProjectDocuments")]
        public async Task<ActionResult<ProjectDocumentResponseDto>> Get(int id) => Ok(await _mediator.Send(new GetProjectDocumentQuery(id)));

        /// <summary>اعتماد إصدار (من رفعه لا يعتمده)</summary>
        [HttpPost("Approve/{versionId}")]
        [Authorize(Policy = "ApproveProjectDocument")]
        public async Task<ActionResult<ProjectDocumentResponseDto>> Approve(int versionId)
            => Ok(await _mediator.Send(new ApproveProjectDocumentVersionCommand(versionId)));

        /// <summary>حذف وثيقة لا إصدار معتمداً فيها</summary>
        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteProjectDocument")]
        public async Task<IActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteProjectDocumentCommand(id));
            return NoContent();
        }

        /// <summary>
        /// ملف الإصدار بنطاقات (Range): يُستأنف التنزيل المنقطع ويُفتح PDF الكبير فوراً. الصورة وPDF تُعرض داخل الصفحة،
        /// وغيرها (أو مع ?download=true) تُنزَّل. فتح الإصدار المعتمد يُسجَّل في التدقيق (مرة لكل فتح لا لكل نطاق).
        /// </summary>
        [HttpGet("Version/{versionId}/File")]
        [Authorize(Policy = "DownloadProjectDocument")]
        public async Task<IActionResult> File(int versionId, [FromQuery] bool download = false)
        {
            var first = ProjectDocumentRules.IsFirstRequest(Request.Headers.Range.ToString());
            var file = await _mediator.Send(new GetProjectDocumentFileQuery(versionId, download, first));
            Response.Headers.CacheControl = "private, no-store";
            return file.Inline
                ? base.File(file.Content, file.ContentType, enableRangeProcessing: true)
                : base.File(file.Content, file.ContentType, file.FileName, enableRangeProcessing: true);
        }
    }
}
