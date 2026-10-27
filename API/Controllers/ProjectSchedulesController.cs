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

        // ───────── التأخير ─────────

        [HttpGet("Delays/{projectId}")]
        [Authorize(Policy = "AnyScheduleView")]
        public async Task<ActionResult<List<ScheduleDelayResponseDto>>> Delays(int projectId)
            => Ok(await _mediator.Send(new GetScheduleDelaysQuery(projectId)));

        /// <summary>تفصيل تأخير، ومعه لواحق مقترحة للمراجعة إن كان حيّاً</summary>
        [HttpGet("Delay/{id}")]
        [Authorize(Policy = "AnyScheduleView")]
        public async Task<ActionResult<ScheduleDelayResponseDto>> Delay(int id)
            => Ok(await _mediator.Send(new GetScheduleDelayQuery(id)));

        /// <summary>تسجيل تأخير على بند من الجدول المعتمد. مرور التاريخ وحده لا يُنشئه: يسجّله مخوَّل بعد مراجعة الحالة</summary>
        [HttpPost("RecordDelay/{taskId}")]
        [Authorize(Policy = "RecordScheduleDelay")]
        public async Task<ActionResult<ScheduleDelayResponseDto>> RecordDelay(int taskId, [FromBody] ScheduleDelayRequestDto dto)
            => Ok(await _mediator.Send(new RecordScheduleDelayCommand(taskId, dto)));

        /// <summary>رفع تبرير التأخير للاعتماد (من سجّله وحده): الجهة الموافقة وتاريخها ومستند الموافقة</summary>
        [HttpPut("Delay/{id}/Justify")]
        [Authorize(Policy = "RecordScheduleDelay")]
        public async Task<ActionResult<ScheduleDelayResponseDto>> Justify(int id, [FromBody] JustifyScheduleDelayRequestDto dto)
            => Ok(await _mediator.Send(new JustifyScheduleDelayCommand(id, dto)));

        /// <summary>اعتماد التبرير أو رفضه (غير من سجّل التأخير؛ الرفض بسبب)</summary>
        [HttpPut("Delay/{id}/Decide")]
        [Authorize(Policy = "ApproveDelayJustification")]
        public async Task<ActionResult<ScheduleDelayResponseDto>> DecideDelay(int id, [FromBody] ScheduleDecisionRequestDto dto)
            => Ok(await _mediator.Send(new DecideScheduleDelayCommand(id, dto)));

        // ───────── التمديد ─────────

        [HttpGet("Extensions/{projectId}")]
        [Authorize(Policy = "AnyScheduleView")]
        public async Task<ActionResult<List<ScheduleExtensionResponseDto>>> Extensions(int projectId)
            => Ok(await _mediator.Send(new GetScheduleExtensionsQuery(projectId)));

        /// <summary>تفصيل طلب: بنوده ومستنداته، وقبل البتّ فيه تعارضات التبعيات واللواحق المقترحة وتقادم التواريخ</summary>
        [HttpGet("Extension/{id}")]
        [Authorize(Policy = "AnyScheduleView")]
        public async Task<ActionResult<ScheduleExtensionResponseDto>> Extension(int id)
            => Ok(await _mediator.Send(new GetScheduleExtensionQuery(id)));

        /// <summary>معاينة نتيجة التمديد على التواريخ الحالية دون حفظ شيء</summary>
        [HttpPost("Extensions/{projectId}/Preview")]
        [Authorize(Policy = "RequestScheduleExtension")]
        public async Task<ActionResult<ScheduleExtensionPreviewResponseDto>> PreviewExtension(int projectId, [FromBody] ScheduleExtensionRequestDto dto)
            => Ok(await _mediator.Send(new PreviewScheduleExtensionCommand(projectId, dto)));

        /// <summary>إعداد طلب تمديد (مسودة). طلب واحد غير مبتوت فيه لكل مشروع</summary>
        [HttpPost("Extensions/{projectId}")]
        [Authorize(Policy = "RequestScheduleExtension")]
        public async Task<ActionResult<ScheduleExtensionResponseDto>> CreateExtension(int projectId, [FromBody] ScheduleExtensionRequestDto dto)
            => Ok(await _mediator.Send(new CreateScheduleExtensionCommand(projectId, dto)));

        [HttpPut("Extension/{id}")]
        [Authorize(Policy = "RequestScheduleExtension")]
        public async Task<ActionResult<ScheduleExtensionResponseDto>> UpdateExtension(int id, [FromBody] ScheduleExtensionRequestDto dto)
            => Ok(await _mediator.Send(new UpdateScheduleExtensionCommand(id, dto)));

        [HttpPost("Extension/{id}/Submit")]
        [Authorize(Policy = "RequestScheduleExtension")]
        public async Task<ActionResult<ScheduleExtensionResponseDto>> SubmitExtension(int id)
            => Ok(await _mediator.Send(new SubmitScheduleExtensionCommand(id)));

        [HttpDelete("Extension/{id}")]
        [Authorize(Policy = "RequestScheduleExtension")]
        public async Task<IActionResult> DeleteExtension(int id)
        {
            await _mediator.Send(new DeleteScheduleExtensionCommand(id));
            return NoContent();
        }

        /// <summary>اعتماد التمديد (يغيّر التواريخ الحالية دفعة واحدة والأصلية ثابتة) أو رفضه (بسبب) — غير صاحب الطلب</summary>
        [HttpPut("Extension/{id}/Decide")]
        [Authorize(Policy = "ApproveScheduleExtension")]
        public async Task<ActionResult<ScheduleExtensionResponseDto>> DecideExtension(int id, [FromBody] ScheduleDecisionRequestDto dto)
            => Ok(await _mediator.Send(new DecideScheduleExtensionCommand(id, dto)));
    }
}
