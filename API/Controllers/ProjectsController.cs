using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Projects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// المشاريع وإضبارتها. الصلاحية تفتح العملية، وحدّ المشاريع ثابت في ProjectRules.Visible (مشاريعي ← قسمي ← فرعي ← المؤسسة)
    /// ويُفحص داخل المعالج لكل مشروع. لا حذف للمشاريع: تُلغى بتغيير مرحلتها.
    /// </summary>
    [ApiController]
    [Route("api/Projects")]
    [Authorize]
    public class ProjectsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ProjectsController(IMediator mediator) => _mediator = mediator;

        /// <summary>بحث مرقّم (الأحدث أولاً): search = اسم أو رقم مرجعي أو جهة مستفيدة أو PRJ-…</summary>
        [HttpGet("Search")]
        [Authorize(Policy = "AnyProjectView")]
        public async Task<ActionResult<PagedResultDto<ProjectResponseDto>>> Search(
            [FromQuery] string? search, [FromQuery] int? branchId, [FromQuery] int? departmentId, [FromQuery] int? siteId,
            [FromQuery] int? stage, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(await _mediator.Send(new SearchProjectsQuery(search, branchId, departmentId, siteId, stage, page, pageSize)));

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "AnyProjectView")]
        public async Task<ActionResult<ProjectResponseDto>> Get(int id) => Ok(await _mediator.Send(new GetProjectQuery(id)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateProject")]
        public async Task<ActionResult<ProjectResponseDto>> Create([FromBody] ProjectRequestDto dto)
            => Ok(await _mediator.Send(new CreateProjectCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditProject")]
        public async Task<ActionResult<ProjectResponseDto>> Update(int id, [FromBody] ProjectRequestDto dto)
            => Ok(await _mediator.Send(new UpdateProjectCommand(id, dto)));

        /// <summary>موظفون يصلحون مشاركين (من فرع المشروع ولم يُضافوا) للبحث بالاسم</summary>
        [HttpGet("MemberCandidates/{id}")]
        [Authorize(Policy = "EditProject")]
        public async Task<ActionResult<List<ProjectMemberCandidateDto>>> MemberCandidates(int id, [FromQuery] string? search)
            => Ok(await _mediator.Send(new GetProjectMemberCandidatesQuery(id, search)));

        [HttpPost("Members/{id}")]
        [Authorize(Policy = "EditProject")]
        public async Task<ActionResult<ProjectResponseDto>> AddMember(int id, [FromBody] AddProjectMemberRequestDto dto)
            => Ok(await _mediator.Send(new AddProjectMemberCommand(id, dto)));

        [HttpDelete("Members/{id}/{userId}")]
        [Authorize(Policy = "EditProject")]
        public async Task<ActionResult<ProjectResponseDto>> RemoveMember(int id, int userId)
            => Ok(await _mediator.Send(new RemoveProjectMemberCommand(id, userId)));

        /// <summary>سجل تدقيق المشروع (الأحدث أولاً)؛ entityType = Project أو ProjectDocument أو ProjectDocumentVersion</summary>
        [HttpGet("Audit/{id}")]
        [Authorize(Policy = "ViewProjectAudit")]
        public async Task<ActionResult<PagedResultDto<AuditLogResponseDto>>> Audit(
            int id, [FromQuery] string? entityType, [FromQuery] int page = 1, [FromQuery] int pageSize = 30)
            => Ok(await _mediator.Send(new GetProjectAuditQuery(id, entityType, page, pageSize)));
    }
}
