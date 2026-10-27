using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Schedules;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// أمر المباشرة والجدول الزمني للمشروع. الصلاحية تفتح العملية، وحدّ السجلات هو حدّ رؤية المشروع (ProjectRules.Visible)
    /// ويُفحص داخل المعالج. المسودة تُحرَّر وتُعتمد؛ المعتمد لا تُعدَّل تواريخه إلا بتمديد معتمد (الخطوة 1.3)، ويُحدَّث إنجازه فقط.
    /// </summary>
    [ApiController]
    [Route("api/ProjectSchedules")]
    [Authorize]
    public class ProjectSchedulesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ProjectSchedulesController(IMediator mediator) => _mediator = mediator;

        // ───────── القراءة ─────────

        /// <summary>أمر المباشرة وإصدارات الجدول ومؤشرات المعتمد (نسبة الإنجاز والمخطط وخط الأساس ومرشَّحو التأخر)</summary>
        [HttpGet("Overview/{projectId}")]
        [Authorize(Policy = "AnyScheduleView")]
        public async Task<ActionResult<ScheduleOverviewResponseDto>> Overview(int projectId)
            => Ok(await _mediator.Send(new GetScheduleOverviewQuery(projectId)));

        /// <summary>إصدار كامل: بنوده وتبعياته ومؤشراته وتنبيهاته وما يستطيع المستخدم فعله</summary>
        [HttpGet("Get/{id}")]
        [Authorize(Policy = "AnyScheduleView")]
        public async Task<ActionResult<ScheduleDetailResponseDto>> Get(int id)
            => Ok(await _mediator.Send(new GetScheduleQuery(id)));

        /// <summary>سجل تحديثات إنجاز بند (الأحدث أولاً)</summary>
        [HttpGet("Progress/{taskId}")]
        [Authorize(Policy = "AnyScheduleView")]
        public async Task<ActionResult<List<ScheduleProgressEntryResponseDto>>> ProgressHistory(int taskId)
            => Ok(await _mediator.Send(new GetScheduleProgressHistoryQuery(taskId)));

        // ───────── أمر المباشرة ─────────

        [HttpPut("StartOrder/{projectId}")]
        [Authorize(Policy = "EditProjectStartOrder")]
        public async Task<ActionResult<StartOrderResponseDto>> SaveStartOrder(int projectId, [FromBody] StartOrderRequestDto dto)
            => Ok(await _mediator.Send(new SaveStartOrderCommand(projectId, dto)));

        // ───────── المسودة ─────────

        /// <summary>مسودة جديدة: فارغة، أو نسخة من الجدول المعتمد بتواريخه الأصلية إن وُجد</summary>
        [HttpPost("Draft/{projectId}")]
        [Authorize(Policy = "EditProjectSchedule")]
        public async Task<ActionResult<ScheduleDetailResponseDto>> CreateDraft(int projectId, [FromBody] ScheduleDraftRequestDto dto)
            => Ok(await _mediator.Send(new CreateScheduleDraftCommand(projectId, dto)));

        [HttpDelete("Draft/{id}")]
        [Authorize(Policy = "EditProjectSchedule")]
        public async Task<IActionResult> DeleteDraft(int id)
        {
            await _mediator.Send(new DeleteScheduleDraftCommand(id));
            return NoContent();
        }

        /// <summary>اعتماد المسودة: يثبّت التواريخ الأصلية ويحلّ محل المعتمد السابق (من أنشأها لا يعتمدها)</summary>
        [HttpPost("Approve/{id}")]
        [Authorize(Policy = "ApproveProjectSchedule")]
        public async Task<ActionResult<ScheduleDetailResponseDto>> Approve(int id)
            => Ok(await _mediator.Send(new ApproveScheduleCommand(id)));

        // ───────── البنود والتبعيات ─────────

        [HttpPost("Tasks/{scheduleId}")]
        [Authorize(Policy = "EditProjectSchedule")]
        public async Task<ActionResult<ScheduleTaskResponseDto>> AddTask(int scheduleId, [FromBody] ScheduleTaskRequestDto dto)
            => Ok(await _mediator.Send(new AddScheduleTaskCommand(scheduleId, dto)));

        [HttpPut("Task/{taskId}")]
        [Authorize(Policy = "EditProjectSchedule")]
        public async Task<ActionResult<ScheduleTaskResponseDto>> UpdateTask(int taskId, [FromBody] ScheduleTaskRequestDto dto)
            => Ok(await _mediator.Send(new UpdateScheduleTaskCommand(taskId, dto)));

        [HttpDelete("Task/{taskId}")]
        [Authorize(Policy = "EditProjectSchedule")]
        public async Task<IActionResult> DeleteTask(int taskId)
        {
            await _mediator.Send(new DeleteScheduleTaskCommand(taskId));
            return NoContent();
        }

        [HttpPut("Reorder/{scheduleId}")]
        [Authorize(Policy = "EditProjectSchedule")]
        public async Task<IActionResult> Reorder(int scheduleId, [FromBody] ReorderScheduleTasksRequestDto dto)
        {
            await _mediator.Send(new ReorderScheduleTasksCommand(scheduleId, dto));
            return NoContent();
        }

        [HttpPost("Dependencies/{scheduleId}")]
        [Authorize(Policy = "EditProjectSchedule")]
        public async Task<ActionResult<ScheduleDependencyResponseDto>> AddDependency(int scheduleId, [FromBody] ScheduleDependencyRequestDto dto)
            => Ok(await _mediator.Send(new AddScheduleDependencyCommand(scheduleId, dto)));

        [HttpDelete("Dependency/{id}")]
        [Authorize(Policy = "EditProjectSchedule")]
        public async Task<IActionResult> RemoveDependency(int id)
        {
            await _mediator.Send(new RemoveScheduleDependencyCommand(id));
            return NoContent();
        }

        // ───────── الإنجاز والتصنيف ─────────

        /// <summary>تحديث حالة بند معتمد ونسبته (يُسجَّل في سجل الإنجاز)</summary>
        [HttpPut("Progress/{taskId}")]
        [Authorize(Policy = "UpdateScheduleProgress")]
        public async Task<ActionResult<ScheduleTaskResponseDto>> UpdateProgress(int taskId, [FromBody] UpdateScheduleProgressRequestDto dto)
            => Ok(await _mediator.Send(new UpdateScheduleProgressCommand(taskId, dto)));

        /// <summary>تصنيف البند حرجاً أو غير حرج (يدوي)</summary>
        [HttpPut("Classify/{taskId}")]
        [Authorize(Policy = "ClassifyScheduleTask")]
        public async Task<ActionResult<ScheduleTaskResponseDto>> Classify(int taskId, [FromBody] ClassifyScheduleTaskRequestDto dto)
            => Ok(await _mediator.Send(new ClassifyScheduleTaskCommand(taskId, dto)));
    }
}
