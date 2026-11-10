using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.CentralStudies.ActivityLogs.Queries.GetByProject;
using Application.Features.CentralStudies.ActivityLogs.Queries.GetByRequest;
using Application.Features.CentralStudies.Projects.Commands.AddDepartment;
using Application.Features.CentralStudies.Projects.Commands.AddMember;
using Application.Features.CentralStudies.Projects.Commands.Cancel;
using Application.Features.CentralStudies.Projects.Commands.Create;
using Application.Features.CentralStudies.Projects.Commands.Delete;
using Application.Features.CentralStudies.Projects.Commands.RemoveDepartment;
using Application.Features.CentralStudies.Projects.Commands.RemoveMember;
using Application.Features.CentralStudies.Projects.Commands.RequestCompletion;
using Application.Features.CentralStudies.Projects.Commands.Update;
using Application.Features.CentralStudies.Projects.Queries.GetById;
using Application.Features.CentralStudies.Projects.Queries.Search;
using Application.Features.CentralStudies.Stages.Commands.Close;
using Application.Features.CentralStudies.Stages.Commands.Create;
using Application.Features.CentralStudies.Stages.Commands.Delete;
using Application.Features.CentralStudies.Stages.Commands.Reopen;
using Application.Features.CentralStudies.Stages.Commands.Reorder;
using Application.Features.CentralStudies.Stages.Commands.Update;
using Application.Features.CentralStudies.Stages.Queries.GetByProject;
using Application.Features.CentralStudies.Tasks.Commands.Create;
using Application.Features.CentralStudies.Tasks.Commands.Delete;
using Application.Features.CentralStudies.Tasks.Commands.Update;
using Application.Features.CentralStudies.Tasks.Commands.UpdateProgress;
using Application.Features.CentralStudies.Tasks.Queries.GetById;
using Application.Features.CentralStudies.Tasks.Queries.GetByProject;
using Application.Features.CentralStudies.Tasks.Queries.GetByStage;
using Application.Features.CentralStudies.Tasks.Queries.GetMine;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// مشاريع الدراسات: المشروع وأقسامه المشاركة وفريقه وطلب اعتماد الإنجاز وسجله. حدّ الصلاحيات: مشاريع فرعه.
    /// </summary>
    [ApiController]
    [Route("api/StudyProjects")]
    [Authorize]
    public class StudyProjectsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public StudyProjectsController(IMediator mediator) => _mediator = mediator;

        /// <summary>بحث: status، search (الكود أو الاسم)، branchId، from، to (البدء المخطط)، page، pageSize</summary>
        [HttpGet("GetAll")]
        [Authorize(Policy = "ViewStudyProjects")]
        public async Task<ActionResult<PagedResultDto<StudyProjectDto>>> GetAll([FromQuery] StudyDocumentFilterDto filter)
            => Ok(await _mediator.Send(new SearchStudyProjectsQuery(filter)));

        /// <summary>المشروع مع الأقسام والأعضاء والمراحل (ونسب التقدم) والسلسلة وتواقيع الإنجاز</summary>
        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewStudyProjects")]
        public async Task<ActionResult<StudyProjectDto>> Get(int id) => Ok(await _mediator.Send(new GetStudyProjectQuery(id)));

        /// <summary>من أمر بدء معتمد: تُنشأ المراحل من أنواع المراحل الفعّالة ويُضاف القسم المختص مشاركاً</summary>
        [HttpPost("Create")]
        [Authorize(Policy = "CreateStudyProject")]
        public async Task<ActionResult<StudyProjectDto>> Create([FromBody] StudyProjectFormDto dto)
            => Ok(await _mediator.Send(new CreateStudyProjectCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditStudyProject")]
        public async Task<ActionResult<StudyProjectDto>> Update(int id, [FromBody] StudyProjectFormDto dto)
            => Ok(await _mediator.Send(new UpdateStudyProjectCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteStudyProject")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteStudyProjectCommand(id));
            return Ok(new { message = "تم حذف المشروع" });
        }

        [HttpPut("Cancel/{id}")]
        [Authorize(Policy = "CancelStudyDocuments")]
        public async Task<ActionResult<StudyProjectDto>> Cancel(int id, [FromBody] StudyCancelDto dto)
            => Ok(await _mediator.Send(new CancelStudyProjectCommand(id, dto.Reason)));

        /// <summary>بعد إغلاق كل المراحل: يُرسل الإنجاز لتوقيع إدارة المشاريع ثم رئيس الفرع (يكتمل المشروع بالموافقتين)</summary>
        [HttpPut("RequestCompletion/{id}")]
        [Authorize(Policy = "EditStudyProject")]
        public async Task<ActionResult<StudyProjectDto>> RequestCompletion(int id)
            => Ok(await _mediator.Send(new RequestStudyProjectCompletionCommand(id)));

        // ════════ الأقسام المشاركة والفريق ════════

        [HttpPost("Departments/{projectId}")]
        [Authorize(Policy = "AddStudyProjectDepartment")]
        public async Task<ActionResult<StudyProjectDto>> AddDepartment(int projectId, [FromBody] ProjectDepartmentFormDto dto)
            => Ok(await _mediator.Send(new AddStudyProjectDepartmentCommand(projectId, dto.DepartmentId)));

        /// <summary>projectDepartmentId = Id المشاركة (Departments[].Id في تفاصيل المشروع)</summary>
        [HttpDelete("Departments/{projectDepartmentId}")]
        [Authorize(Policy = "RemoveStudyProjectDepartment")]
        public async Task<ActionResult<StudyProjectDto>> RemoveDepartment(int projectDepartmentId)
            => Ok(await _mediator.Send(new RemoveStudyProjectDepartmentCommand(projectDepartmentId)));

        /// <summary>رئيس القسم المشارك يعيّن موظفاً من قسمه</summary>
        [HttpPost("Members/{projectDepartmentId}")]
        [Authorize(Policy = "AddStudyProjectMember")]
        public async Task<ActionResult<StudyProjectDto>> AddMember(int projectDepartmentId, [FromBody] ProjectMemberFormDto dto)
            => Ok(await _mediator.Send(new AddStudyProjectMemberCommand(projectDepartmentId, dto)));

        [HttpDelete("Members/{projectDepartmentId}/{userId}")]
        [Authorize(Policy = "RemoveStudyProjectMember")]
        public async Task<ActionResult<StudyProjectDto>> RemoveMember(int projectDepartmentId, int userId)
            => Ok(await _mediator.Send(new RemoveStudyProjectMemberCommand(projectDepartmentId, userId)));

        /// <summary>سجل الدراسة كاملاً (من الطلب حتى الآن)</summary>
        [HttpGet("ActivityLog/{id}")]
        [Authorize(Policy = "ViewStudyActivityLog")]
        public async Task<ActionResult<PagedResultDto<StudyActivityLogDto>>> ActivityLog(int id,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
            => Ok(await _mediator.Send(new GetProjectActivityLogQuery(id, page, pageSize)));
    }

    /// <summary>مراحل المشاريع: متتالية، تغلقها إدارة المشاريع بالترتيب (CloseStudyStage)</summary>
    [ApiController]
    [Route("api/StudyStages")]
    [Authorize]
    public class StudyStagesController : ControllerBase
    {
        private readonly IMediator _mediator;
        public StudyStagesController(IMediator mediator) => _mediator = mediator;

        [HttpGet("ByProject/{projectId}")]
        [Authorize(Policy = "ViewStudyProjects")]
        public async Task<ActionResult<List<ProjectStageDto>>> ByProject(int projectId)
            => Ok(await _mediator.Send(new GetStudyStagesQuery(projectId)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateStudyStage")]
        public async Task<ActionResult<ProjectStageDto>> Create([FromBody] StudyStageFormDto dto)
            => Ok(await _mediator.Send(new CreateStudyStageCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditStudyStage")]
        public async Task<ActionResult<ProjectStageDto>> Update(int id, [FromBody] StudyStageFormDto dto)
            => Ok(await _mediator.Send(new UpdateStudyStageCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteStudyStage")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteStudyStageCommand(id));
            return Ok(new { message = "تم حذف المرحلة" });
        }

        /// <summary>كل مراحل المشروع بالترتيب الجديد (المغلقة تبقى قبل المفتوحة)</summary>
        [HttpPut("Reorder/{projectId}")]
        [Authorize(Policy = "EditStudyStage")]
        public async Task<ActionResult<List<ProjectStageDto>>> Reorder(int projectId, [FromBody] ReorderStudyStagesDto dto)
            => Ok(await _mediator.Send(new ReorderStudyStagesCommand(projectId, dto.StageIds)));

        /// <summary>تُغلق بعد إغلاق ما قبلها وإنجاز كل مهامها</summary>
        [HttpPut("Close/{id}")]
        [Authorize(Policy = "CloseStudyStage")]
        public async Task<ActionResult<ProjectStageDto>> Close(int id) => Ok(await _mediator.Send(new CloseStudyStageCommand(id)));

        /// <summary>يُعاد فتح آخر مرحلة مغلقة فقط (لا مراحل المستندات)</summary>
        [HttpPut("Reopen/{id}")]
        [Authorize(Policy = "CloseStudyStage")]
        public async Task<ActionResult<ProjectStageDto>> Reopen(int id) => Ok(await _mediator.Send(new ReopenStudyStageCommand(id)));
    }

    /// <summary>مهام مراحل المشاريع (منفصلة عن لوحة المهام): الإسناد لقسم مشارك أو مكتب منه أو عضو في الفريق</summary>
    [ApiController]
    [Route("api/StudyTasks")]
    [Authorize]
    public class StudyTasksController : ControllerBase
    {
        private readonly IMediator _mediator;
        public StudyTasksController(IMediator mediator) => _mediator = mediator;

        [HttpGet("ByStage/{stageId}")]
        [Authorize(Policy = "ViewStudyProjects")]
        public async Task<ActionResult<List<StageTaskDto>>> ByStage(int stageId)
            => Ok(await _mediator.Send(new GetStudyTasksQuery(stageId)));

        /// <summary>كل مهام المشروع في طلب واحد (لمخطط Gantt): بترتيب المرحلة ثم البدء المخطط، والأعلام كما في ByStage</summary>
        [HttpGet("ByProject/{projectId}")]
        [Authorize(Policy = "ViewStudyProjects")]
        public async Task<ActionResult<List<StageTaskDto>>> ByProject(int projectId)
        {
            var result = await _mediator.Send(new GetStudyProjectTasksQuery(projectId));
            // الرد قائمة كما هو؛ العدد الكلي وما إن كان مقطوعاً (الحد 2000 مهمة) في الترويسة
            Response.Headers["X-Total-Count"] = result.Total.ToString();
            Response.Headers["X-Truncated"] = result.Truncated ? "true" : "false";
            return Ok(result.Items);
        }

        /// <summary>لمن يرى مشاريع فرعه، أو للمكلَّف بها</summary>
        [HttpGet("Get/{id}")]
        [Authorize(Policy = "AnyStudyTaskView")]
        public async Task<ActionResult<StageTaskDto>> Get(int id) => Ok(await _mediator.Send(new GetStudyTaskQuery(id)));

        /// <summary>مهامي: المسندة إليّ أو إلى مكتبي أو قسمي (openOnly = غير المنجزة)</summary>
        [HttpGet("My")]
        [Authorize(Policy = "UpdateStudyTaskProgress")]
        public async Task<ActionResult<PagedResultDto<StageTaskDto>>> My([FromQuery] bool openOnly = true,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(await _mediator.Send(new GetMyStudyTasksQuery(openOnly, page, pageSize)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateStudyTask")]
        public async Task<ActionResult<StageTaskDto>> Create([FromBody] StudyTaskFormDto dto)
            => Ok(await _mediator.Send(new CreateStudyTaskCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditStudyTask")]
        public async Task<ActionResult<StageTaskDto>> Update(int id, [FromBody] StudyTaskFormDto dto)
            => Ok(await _mediator.Send(new UpdateStudyTaskCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteStudyTask")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteStudyTaskCommand(id));
            return Ok(new { message = "تم حذف المهمة" });
        }

        /// <summary>المكلَّف يحدّث النسبة والحالة (منجزة = 100%)</summary>
        [HttpPut("Progress/{id}")]
        [Authorize(Policy = "UpdateStudyTaskProgress")]
        public async Task<ActionResult<StageTaskDto>> Progress(int id, [FromBody] StudyTaskProgressDto dto)
            => Ok(await _mediator.Send(new UpdateStudyTaskProgressCommand(id, dto)));
    }

    /// <summary>سجل الدراسة من الطلب</summary>
    [ApiController]
    [Route("api/StudyActivityLogs")]
    [Authorize]
    public class StudyActivityLogsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public StudyActivityLogsController(IMediator mediator) => _mediator = mediator;

        [HttpGet("ByRequest/{requestId}")]
        [Authorize(Policy = "ViewStudyActivityLog")]
        public async Task<ActionResult<PagedResultDto<StudyActivityLogDto>>> ByRequest(int requestId,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
            => Ok(await _mediator.Send(new GetStudyActivityLogQuery(requestId, page, pageSize)));
    }
}
