using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.CentralStudies.Lookups;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// فرع الدراسات — القوائم: أنواع المراحل، أنواع المرفقات، الاختصاصات الهندسية، وقيم التعدادات للواجهة.
    /// العرض ViewStudyLookups، والإدارة Create/Edit/DeleteStudyLookup.
    /// </summary>
    [ApiController]
    [Route("api/StudyLookups")]
    [Authorize]
    public class StudyLookupsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public StudyLookupsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>كل تعدادات فرع الدراسات: القيمة الرقمية واسمها العربي</summary>
        [HttpGet("Enums")]
        [Authorize(Policy = "ViewStudyLookups")]
        public async Task<ActionResult<Dictionary<string, List<StudyEnumValueDto>>>> Enums()
            => Ok(await _mediator.Send(new GetStudyEnumsQuery()));

        /// <summary>
        /// موظفو فرع الدراسة النشطون لقوائم الاختيار: id، fullName، departmentId، departmentName، officeId، officeName فقط.
        /// branchId لمن يتصفح المؤسسة (غيره فرعه دائماً)، departmentId لموظفي قسم واحد (إضافة أعضاء القسم المشارك).
        /// </summary>
        [HttpGet("Employees")]
        [Authorize(Policy = "AnyStudyEmployeePick")]
        public async Task<ActionResult<List<StudyEmployeeDto>>> Employees([FromQuery] int? branchId, [FromQuery] int? departmentId)
            => Ok(await _mediator.Send(new GetStudyEmployeesQuery(branchId, departmentId)));

        // ════════ أنواع المراحل ════════

        [HttpGet("StageTypes/GetAll")]
        [Authorize(Policy = "ViewStudyLookups")]
        public async Task<ActionResult<List<StudyStageTypeDto>>> GetStageTypes([FromQuery] bool activeOnly = false)
            => Ok(await _mediator.Send(new GetStudyStageTypesQuery(activeOnly)));

        [HttpPost("StageTypes/Create")]
        [Authorize(Policy = "CreateStudyLookup")]
        public async Task<ActionResult<StudyStageTypeDto>> CreateStageType([FromBody] StudyStageTypeFormDto dto)
            => Ok(await _mediator.Send(new CreateStudyStageTypeCommand(dto)));

        [HttpPut("StageTypes/Update/{id}")]
        [Authorize(Policy = "EditStudyLookup")]
        public async Task<ActionResult<StudyStageTypeDto>> UpdateStageType(int id, [FromBody] StudyStageTypeFormDto dto)
            => Ok(await _mediator.Send(new UpdateStudyStageTypeCommand(id, dto)));

        [HttpDelete("StageTypes/Delete/{id}")]
        [Authorize(Policy = "DeleteStudyLookup")]
        public async Task<ActionResult> DeleteStageType(int id)
        {
            await _mediator.Send(new DeleteStudyStageTypeCommand(id));
            return Ok(new { message = "تم حذف نوع المرحلة" });
        }

        // ════════ أنواع المرفقات ════════

        [HttpGet("AttachmentTypes/GetAll")]
        [Authorize(Policy = "AnyStudyView")]
        public async Task<ActionResult<List<StudyLookupDto>>> GetAttachmentTypes([FromQuery] bool activeOnly = false)
            => Ok(await _mediator.Send(new GetStudyLookupsQuery(StudyLookupKind.AttachmentType, activeOnly)));

        [HttpPost("AttachmentTypes/Create")]
        [Authorize(Policy = "CreateStudyLookup")]
        public async Task<ActionResult<StudyLookupDto>> CreateAttachmentType([FromBody] StudyLookupFormDto dto)
            => Ok(await _mediator.Send(new CreateStudyLookupCommand(StudyLookupKind.AttachmentType, dto)));

        [HttpPut("AttachmentTypes/Update/{id}")]
        [Authorize(Policy = "EditStudyLookup")]
        public async Task<ActionResult<StudyLookupDto>> UpdateAttachmentType(int id, [FromBody] StudyLookupFormDto dto)
            => Ok(await _mediator.Send(new UpdateStudyLookupCommand(StudyLookupKind.AttachmentType, id, dto)));

        [HttpDelete("AttachmentTypes/Delete/{id}")]
        [Authorize(Policy = "DeleteStudyLookup")]
        public async Task<ActionResult> DeleteAttachmentType(int id)
        {
            await _mediator.Send(new DeleteStudyLookupCommand(StudyLookupKind.AttachmentType, id));
            return Ok(new { message = "تم حذف نوع المرفق" });
        }

        // ════════ الاختصاصات الهندسية ════════

        [HttpGet("Disciplines/GetAll")]
        [Authorize(Policy = "ViewStudyLookups")]
        public async Task<ActionResult<List<StudyLookupDto>>> GetDisciplines([FromQuery] bool activeOnly = false)
            => Ok(await _mediator.Send(new GetStudyLookupsQuery(StudyLookupKind.Discipline, activeOnly)));

        [HttpPost("Disciplines/Create")]
        [Authorize(Policy = "CreateStudyLookup")]
        public async Task<ActionResult<StudyLookupDto>> CreateDiscipline([FromBody] StudyLookupFormDto dto)
            => Ok(await _mediator.Send(new CreateStudyLookupCommand(StudyLookupKind.Discipline, dto)));

        [HttpPut("Disciplines/Update/{id}")]
        [Authorize(Policy = "EditStudyLookup")]
        public async Task<ActionResult<StudyLookupDto>> UpdateDiscipline(int id, [FromBody] StudyLookupFormDto dto)
            => Ok(await _mediator.Send(new UpdateStudyLookupCommand(StudyLookupKind.Discipline, id, dto)));

        [HttpDelete("Disciplines/Delete/{id}")]
        [Authorize(Policy = "DeleteStudyLookup")]
        public async Task<ActionResult> DeleteDiscipline(int id)
        {
            await _mediator.Send(new DeleteStudyLookupCommand(StudyLookupKind.Discipline, id));
            return Ok(new { message = "تم حذف الاختصاص" });
        }
    }
}
