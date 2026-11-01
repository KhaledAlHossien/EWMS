using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Reconciliation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// مطابقة الفواتير والكشوف مع الأسعار (مكتب التسعير): تسجيل المستند ← ربط بنوده بمراجع معتمدة ← مقارنة يحسبها الخادم ← المدقق يسجّل النتيجة
    /// (إحدى ست حالات) ← غيره يعتمد أو يعيد ← إحالة المعتمد. النظام يقارن ويقترح ولا يقرر؛ لا مقارنة بين وحدات أو عملات مختلفة؛ المعتمد لا يُعدَّل.
    /// الحدّ فرع المطابقة (ونظرة المؤسسة كل الفروع) ويُفحص داخل المعالج.
    /// </summary>
    [ApiController]
    [Route("api/PriceReconciliations")]
    [Authorize]
    public class PriceReconciliationsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PriceReconciliationsController(IMediator mediator) => _mediator = mediator;

        /// <summary>بحث مرقّم (الأحدث أولاً). status: 1 مسودة، 2 مطابق … 8 معتمد بعد التدقيق، 9 معاد للتصحيح، 10 ملغى</summary>
        [HttpGet("Search")]
        [Authorize(Policy = "AnyPriceReconciliationView")]
        public async Task<ActionResult<PagedResultDto<ReconciliationListItemDto>>> Search(
            [FromQuery] int? branchId, [FromQuery] int? status, [FromQuery] int? supplierId, [FromQuery] int? projectId, [FromQuery] string? search,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(await _mediator.Send(new SearchReconciliationsQuery(branchId, status, supplierId, projectId, search, page, pageSize)));

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "AnyPriceReconciliationView")]
        public async Task<ActionResult<ReconciliationDetailDto>> Get(int id) => Ok(await _mediator.Send(new GetReconciliationQuery(id)));

        /// <summary>مراجع مقترحة لمورد: أسطر عروض مختارة من طلبات معتمدة وأسعار مرجعية معتمدة للاستخدام الحالي</summary>
        [HttpGet("Candidates")]
        [Authorize(Policy = "PreparePriceReconciliation")]
        public async Task<ActionResult<List<ReconciliationCandidateDto>>> Candidates([FromQuery] int supplierId, [FromQuery] int? branchId, [FromQuery] string? search)
            => Ok(await _mediator.Send(new GetReconciliationCandidatesQuery(branchId, supplierId, search)));

        [HttpPost("Create")]
        [Authorize(Policy = "PreparePriceReconciliation")]
        public async Task<ActionResult<ReconciliationDetailDto>> Create([FromBody] ReconciliationRequestDto dto) => Ok(await _mediator.Send(new CreateReconciliationCommand(dto)));

        /// <summary>تعديل المسودة أو المعاد؛ البنود تُستبدل بالمرسلة وتُجمَّد مراجعها</summary>
        [HttpPut("Update/{id}")]
        [Authorize(Policy = "PreparePriceReconciliation")]
        public async Task<ActionResult<ReconciliationDetailDto>> Update(int id, [FromBody] ReconciliationRequestDto dto) => Ok(await _mediator.Send(new UpdateReconciliationCommand(id, dto)));

        /// <summary>تسجيل نتيجة التدقيق: 2 مطابق، 3 جزئياً، 4 فرق سعر، 5 فرق كمية، 6 بيانات غير مكتملة، 7 يحتاج مراجعة</summary>
        [HttpPost("Record/{id}")]
        [Authorize(Policy = "PreparePriceReconciliation")]
        public async Task<ActionResult<ReconciliationDetailDto>> Record(int id, [FromBody] RecordReconciliationRequestDto dto) => Ok(await _mediator.Send(new RecordReconciliationCommand(id, dto)));

        /// <summary>الاعتماد أو الإعادة للتصحيح (لغير من دقّق)</summary>
        [HttpPost("Decide/{id}")]
        [Authorize(Policy = "ApprovePriceReconciliation")]
        public async Task<ActionResult<ReconciliationDetailDto>> Decide(int id, [FromBody] ReviewReconciliationRequestDto dto) => Ok(await _mediator.Send(new DecideReconciliationCommand(id, dto)));

        /// <summary>إحالة المطابقة المعتمدة إلى الجهة المختصة (تسجيل يدوي)</summary>
        [HttpPost("Refer/{id}")]
        [Authorize(Policy = "ApprovePriceReconciliation")]
        public async Task<ActionResult<ReconciliationDetailDto>> Refer(int id, [FromBody] ReferReconciliationRequestDto dto) => Ok(await _mediator.Send(new ReferReconciliationCommand(id, dto)));

        /// <summary>إلغاء مسودة أو معاد (بديل الحذف)</summary>
        [HttpPost("Cancel/{id}")]
        [Authorize(Policy = "PreparePriceReconciliation")]
        public async Task<ActionResult<ReconciliationDetailDto>> Cancel(int id, [FromBody] PricingStepRequestDto dto) => Ok(await _mediator.Send(new CancelReconciliationCommand(id, dto)));
    }
}
