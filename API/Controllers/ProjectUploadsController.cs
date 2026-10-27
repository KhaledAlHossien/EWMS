using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Projects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// رفع وثائق المشاريع بأجزاء قابلة للاستئناف (ملفات حتى عشرات الجيجابايتات):
    /// Start ← Chunk × N (بايتات خام، كل جزء ≤ ChunkSize) ← Complete ← استعلام الحالة حتى Done/Failed.
    /// الجلسة لصاحبها وحده، والصلاحية UploadProjectDocument على كل نداء.
    /// </summary>
    [ApiController]
    [Route("api/ProjectUploads")]
    [Authorize(Policy = "UploadProjectDocument")]
    public class ProjectUploadsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ProjectUploadsController(IMediator mediator) => _mediator = mediator;

        [HttpPost("Start")]
        public async Task<ActionResult<ProjectUploadResponseDto>> Start([FromBody] StartProjectUploadRequestDto dto)
            => Ok(await _mediator.Send(new StartProjectUploadCommand(dto)));

        /// <summary>
        /// جزء من الملف عند offset (يجب أن يساوي ما استُلم). الجسم بايتات خام (application/octet-stream) تُكتب على القرص مباشرة بلا تحميلها في الذاكرة.
        /// السقف 34MB يغطي أكبر جزء ممكن (Projects:ChunkMegabytes ≤ 32)، والمعالج يرفض الأكبر من الجزء المضبوط.
        /// </summary>
        [HttpPut("{id:guid}/Chunk")]
        [RequestSizeLimit(34L * 1024 * 1024)]
        public async Task<ActionResult<ProjectUploadResponseDto>> Chunk(Guid id, [FromQuery] long offset, CancellationToken ct)
        {
            var length = Request.ContentLength ?? throw new InvalidOperationException("طول الجزء (Content-Length) مطلوب");
            return Ok(await _mediator.Send(new WriteProjectChunkCommand(id, offset, length, Request.Body), ct));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ProjectUploadResponseDto>> Get(Guid id) => Ok(await _mediator.Send(new GetProjectUploadQuery(id)));

        /// <summary>عمليات رفعي (أحدثها أولاً، حتى 50): لاستئناف ما انقطع وعرض نتيجة ما اكتمل</summary>
        [HttpGet("Mine")]
        public async Task<ActionResult<List<ProjectUploadResponseDto>>> Mine([FromQuery] int? projectId)
            => Ok(await _mediator.Send(new ListMyProjectUploadsQuery(projectId)));

        /// <summary>اكتملت الأجزاء: يبدأ الإنهاء في الخلفية (يردّ فوراً بحالة «جارٍ الفحص»)</summary>
        [HttpPost("{id:guid}/Complete")]
        public async Task<ActionResult<ProjectUploadResponseDto>> Complete(Guid id) => Ok(await _mediator.Send(new CompleteProjectUploadCommand(id)));

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            await _mediator.Send(new CancelProjectUploadCommand(id));
            return NoContent();
        }
    }
}
