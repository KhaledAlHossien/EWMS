using Application.DTOs.Response;
using Application.Features.CentralStudies.Print;
using Domain.Entities.CentralStudiesBranch;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// طباعة نماذج الدراسات الورقية (الطلب، التكليف، الرد، أمر البدء، CDA). صلاحية PrintStudyDocuments مع صلاحية عرض المستند،
    /// وحدّها فرع الدراسة. تعيد المستند وترويسة الطباعة والتواقيع بصور نسخها المحفوظة.
    /// </summary>
    [ApiController]
    [Route("api/StudyPrint")]
    [Authorize]
    public class StudyPrintController : ControllerBase
    {
        private readonly IMediator _mediator;
        public StudyPrintController(IMediator mediator) => _mediator = mediator;

        [HttpGet("Request/{id}")]
        [Authorize(Policy = "PrintStudyDocuments")]
        public async Task<ActionResult<StudyPrintDto>> Request(int id) => Ok(await _mediator.Send(new GetStudyPrintQuery(StudyEntityType.Request, id)));

        [HttpGet("Assignment/{id}")]
        [Authorize(Policy = "PrintStudyDocuments")]
        public async Task<ActionResult<StudyPrintDto>> Assignment(int id) => Ok(await _mediator.Send(new GetStudyPrintQuery(StudyEntityType.Assignment, id)));

        [HttpGet("Response/{id}")]
        [Authorize(Policy = "PrintStudyDocuments")]
        public async Task<ActionResult<StudyPrintDto>> Response(int id) => Ok(await _mediator.Send(new GetStudyPrintQuery(StudyEntityType.Response, id)));

        [HttpGet("StartOrder/{id}")]
        [Authorize(Policy = "PrintStudyDocuments")]
        public async Task<ActionResult<StudyPrintDto>> StartOrder(int id) => Ok(await _mediator.Send(new GetStudyPrintQuery(StudyEntityType.StartOrder, id)));

        [HttpGet("DesignApproval/{id}")]
        [Authorize(Policy = "PrintStudyDocuments")]
        public async Task<ActionResult<StudyPrintDto>> DesignApproval(int id) => Ok(await _mediator.Send(new GetStudyPrintQuery(StudyEntityType.DesignApproval, id)));
    }
}
