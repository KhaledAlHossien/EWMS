using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Pricing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// مكتب التسعير — طلبات التسعير: تسجيل ← تكليف موظف ← عروض الموردين ومقارنتها بنداً بنداً ← اختيار بسبب مكتوب ← توصية ← اعتماد (غير من أوصى) ← إرسال النتيجة.
    /// الصلاحية تفتح العملية، والحدّ ثابت في PricingRules (فرع الطلب، ومقدّمه والمكلَّف يريانه) ويُفحص داخل المعالج.
    /// لا مقارنة بين وحدات أو عملات مختلفة، ولا اختيار آلي للأرخص. الطلب لا يُحذف: يُلغى.
    /// </summary>
    [ApiController]
    [Route("api/PricingRequests")]
    [Authorize]
    public class PricingRequestsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PricingRequestsController(IMediator mediator) => _mediator = mediator;

        /// <summary>بحث مرقّم (الأحدث أولاً). status: 1 مسجَّل … 7 ملغى. mine=true: ما سجّلته أو كُلِّفت به</summary>
        [HttpGet("Search")]
        [Authorize(Policy = "AnyPricingRequestView")]
        public async Task<ActionResult<PagedResultDto<PricingListItemDto>>> Search(
            [FromQuery] int? branchId, [FromQuery] int? projectId, [FromQuery] int? status, [FromQuery] string? search,
            [FromQuery] bool overdueOnly = false, [FromQuery] bool mine = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(await _mediator.Send(new SearchPricingRequestsQuery(branchId, projectId, status, search, overdueOnly, mine, page, pageSize)));

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "AnyPricingRequestView")]
        public async Task<ActionResult<PricingDetailDto>> Get(int id) => Ok(await _mediator.Send(new GetPricingRequestQuery(id)));

        /// <summary>موظفو الفرع الذين يصلحون للتكليف (يحملون إعداد العروض) — لمن يكلّف</summary>
        [HttpGet("Assignees")]
        [Authorize(Policy = "AssignPricingRequest")]
        public async Task<ActionResult<List<PricingAssigneeDto>>> Assignees([FromQuery] int? branchId) => Ok(await _mediator.Send(new GetPricingAssigneesQuery(branchId)));

        [HttpPost("Create")]
        [Authorize(Policy = "RegisterPricingRequest")]
        public async Task<ActionResult<PricingDetailDto>> Create([FromBody] PricingRequestRequestDto dto) => Ok(await _mediator.Send(new CreatePricingRequestCommand(dto)));

        /// <summary>تعديل الطلب؛ البنود تُستبدل بالمرسلة وتتجمّد بعد أول عرض</summary>
        [HttpPut("Update/{id}")]
        [Authorize(Policy = "AnyPricingRequestEdit")]
        public async Task<ActionResult<PricingDetailDto>> Update(int id, [FromBody] PricingRequestRequestDto dto) => Ok(await _mediator.Send(new UpdatePricingRequestCommand(id, dto)));

        [HttpPost("Assign/{id}")]
        [Authorize(Policy = "AssignPricingRequest")]
        public async Task<ActionResult<PricingDetailDto>> Assign(int id, [FromBody] AssignPricingRequestDto dto) => Ok(await _mediator.Send(new AssignPricingRequestCommand(id, dto)));

        [HttpPost("Cancel/{id}")]
        [Authorize(Policy = "AnyPricingRequestEdit")]
        public async Task<ActionResult<PricingDetailDto>> Cancel(int id, [FromBody] PricingStepRequestDto dto) => Ok(await _mediator.Send(new CancelPricingRequestCommand(id, dto)));

        // ───────── العروض والمقارنة ─────────

        [HttpPost("Offers/{requestId}")]
        [Authorize(Policy = "PreparePricingOffers")]
        public async Task<ActionResult<PricingDetailDto>> AddOffer(int requestId, [FromBody] PricingOfferRequestDto dto) => Ok(await _mediator.Send(new AddPricingOfferCommand(requestId, dto)));

        /// <summary>تعديل العرض: تُحدَّث الأسعار المرسلة وتُضاف الجديدة وتُحذف ما لم تُرسَل (السطر المختار لا يتغير)</summary>
        [HttpPut("Offer/{offerId}")]
        [Authorize(Policy = "PreparePricingOffers")]
        public async Task<ActionResult<PricingDetailDto>> UpdateOffer(int offerId, [FromBody] PricingOfferRequestDto dto) => Ok(await _mediator.Send(new UpdatePricingOfferCommand(offerId, dto)));

        /// <summary>اختيار سطر لبند بسبب مكتوب: لا يُقبل غير مطابق فنياً ولا بوحدة مخالفة ولا عرض منتهي الصلاحية (ولو الأرخص)</summary>
        [HttpPost("Select/{lineId}")]
        [Authorize(Policy = "PreparePricingOffers")]
        public async Task<ActionResult<PricingDetailDto>> Select(int lineId, [FromBody] SelectPricingLineRequestDto dto) => Ok(await _mediator.Send(new SelectPricingLineCommand(lineId, dto)));

        [HttpPost("Unselect/{lineId}")]
        [Authorize(Policy = "PreparePricingOffers")]
        public async Task<ActionResult<PricingDetailDto>> Unselect(int lineId) => Ok(await _mediator.Send(new UnselectPricingLineCommand(lineId)));

        // ───────── التوصية والاعتماد والإرسال ─────────

        /// <summary>توثيق التوصية: لكل بند سطر مختار؛ يتحول الطلب إلى «بانتظار الاعتماد»</summary>
        [HttpPost("Recommend/{id}")]
        [Authorize(Policy = "PreparePricingOffers")]
        public async Task<ActionResult<PricingDetailDto>> Recommend(int id, [FromBody] PricingStepRequestDto dto) => Ok(await _mediator.Send(new RecommendPricingRequestCommand(id, dto)));

        /// <summary>القرار على التوصية: 1 اعتماد، 2 رفض، 3 إعادة لجمع العروض (الرفض والإعادة بسبب) — غير من أوصى</summary>
        [HttpPost("Decide/{id}")]
        [Authorize(Policy = "ApprovePricingRequest")]
        public async Task<ActionResult<PricingDetailDto>> Decide(int id, [FromBody] DecidePricingRequestDto dto) => Ok(await _mediator.Send(new DecidePricingRequestCommand(id, dto)));

        [HttpPost("SendResult/{id}")]
        [Authorize(Policy = "SendPricingResult")]
        public async Task<ActionResult<PricingDetailDto>> SendResult(int id, [FromBody] SendPricingResultRequestDto dto) => Ok(await _mediator.Send(new SendPricingResultCommand(id, dto)));
    }

    /// <summary>محاضر الاتفاق مع المتعهدين الجدد: الإعداد لمدير مكتب التسعير وحده (صلاحية مستقلة)، والاعتماد لغير من أعدّه. المعتمد لا يُعدَّل.</summary>
    [ApiController]
    [Route("api/PricingAgreements")]
    [Authorize]
    public class PricingAgreementsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PricingAgreementsController(IMediator mediator) => _mediator = mediator;

        /// <summary>status: 1 مسودة، 2 مرسل، 3 معتمد، 4 معاد، 5 ملغى</summary>
        [HttpGet("Search")]
        [Authorize(Policy = "AnyPricingAgreementView")]
        public async Task<ActionResult<PagedResultDto<PricingAgreementListItemDto>>> Search(
            [FromQuery] int? branchId, [FromQuery] int? status, [FromQuery] int? supplierId, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(await _mediator.Send(new SearchPricingAgreementsQuery(branchId, status, supplierId, search, page, pageSize)));

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "AnyPricingAgreementView")]
        public async Task<ActionResult<PricingAgreementDetailDto>> Get(int id) => Ok(await _mediator.Send(new GetPricingAgreementQuery(id)));

        [HttpPost("Create")]
        [Authorize(Policy = "PreparePricingAgreement")]
        public async Task<ActionResult<PricingAgreementDetailDto>> Create([FromBody] PricingAgreementRequestDto dto) => Ok(await _mediator.Send(new CreatePricingAgreementCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "PreparePricingAgreement")]
        public async Task<ActionResult<PricingAgreementDetailDto>> Update(int id, [FromBody] PricingAgreementRequestDto dto) => Ok(await _mediator.Send(new UpdatePricingAgreementCommand(id, dto)));

        /// <summary>الإرسال للاعتماد: يلزم الرقم الرسمي وبند مسعَّر</summary>
        [HttpPost("Submit/{id}")]
        [Authorize(Policy = "PreparePricingAgreement")]
        public async Task<ActionResult<PricingAgreementDetailDto>> Submit(int id, [FromBody] PricingStepRequestDto dto) => Ok(await _mediator.Send(new SubmitPricingAgreementCommand(id, dto)));

        /// <summary>اعتماد المحضر أو إعادته للتصحيح بسبب (غير من أعدّه)</summary>
        [HttpPost("Review/{id}")]
        [Authorize(Policy = "ApprovePricingAgreement")]
        public async Task<ActionResult<PricingAgreementDetailDto>> Review(int id, [FromBody] ReviewPricingAgreementRequestDto dto) => Ok(await _mediator.Send(new ReviewPricingAgreementCommand(id, dto)));

        [HttpPost("Cancel/{id}")]
        [Authorize(Policy = "PreparePricingAgreement")]
        public async Task<ActionResult<PricingAgreementDetailDto>> Cancel(int id, [FromBody] PricingStepRequestDto dto) => Ok(await _mediator.Send(new CancelPricingAgreementCommand(id, dto)));
    }
}
