using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Contracts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// العقود: طبيعة التعاقد وعروضه وإرساؤه ولجنته الفنية والمالية وملاحقه وضمانه ومبالغه المحتجزة وتقييم مقاوله. الصلاحية تفتح العملية،
    /// وحدّ العقود ثابت في ContractRules (حدّ رؤية المشروع نفسه) ويُفحص داخل المعالج. المبالغ يدوية بلا معادلات، والنظام ينبّه ولا يقرر.
    /// العقد لا يُحذف: يُلغى أو يُفسخ. الملاحق والعروض والمحتجزات سجلات تُضاف ولا تُعدَّل.
    /// </summary>
    [ApiController]
    [Route("api/Contracts")]
    [Authorize]
    public class ContractsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ContractsController(IMediator mediator) => _mediator = mediator;

        /// <summary>بحث مرقّم (الأحدث أولاً) ضمن مشاريعي. status: 1 قيد التعاقد … 5 ملغى. method: 1 مباشر، 2 مناقصة، 3 عروض أسعار. warrantyEndingOnly: ينتهي ضمانه قريباً</summary>
        [HttpGet("Search")]
        [Authorize(Policy = "AnyContractView")]
        public async Task<ActionResult<PagedResultDto<ContractListItemDto>>> Search(
            [FromQuery] int? projectId, [FromQuery] int? branchId, [FromQuery] int? status, [FromQuery] int? method, [FromQuery] int? contractorId,
            [FromQuery] string? search, [FromQuery] bool warrantyEndingOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(await _mediator.Send(new SearchContractsQuery(projectId, branchId, status, method, contractorId, search, warrantyEndingOnly, page, pageSize)));

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "AnyContractView")]
        public async Task<ActionResult<ContractDetailDto>> Get(int id) => Ok(await _mediator.Send(new GetContractQuery(id)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateContract")]
        public async Task<ActionResult<ContractDetailDto>> Create([FromBody] ContractRequestDto dto) => Ok(await _mediator.Send(new CreateContractCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditContract")]
        public async Task<ActionResult<ContractDetailDto>> Update(int id, [FromBody] ContractRequestDto dto) => Ok(await _mediator.Send(new UpdateContractCommand(id, dto)));

        /// <summary>تغيير الحالة بقرار: 2 ساري (يشترط اكتمال بيانات التعاقد)، 3 منجز، 4 مفسوخ، 5 ملغى (الفسخ والإلغاء يشترطان سبباً)</summary>
        [HttpPost("Status/{id}")]
        [Authorize(Policy = "EditContract")]
        public async Task<ActionResult<ContractDetailDto>> ChangeStatus(int id, [FromBody] ContractStatusRequestDto dto) => Ok(await _mediator.Send(new ChangeContractStatusCommand(id, dto)));

        // ───────── العروض والإرساء ─────────

        [HttpPost("Offers/{contractId}")]
        [Authorize(Policy = "EditContract")]
        public async Task<ActionResult<ContractDetailDto>> AddOffer(int contractId, [FromBody] ContractOfferRequestDto dto) => Ok(await _mediator.Send(new AddContractOfferCommand(contractId, dto)));

        /// <summary>الإرساء على العرض بالقرار الرسمي: يحدّد المتعاقد معه</summary>
        [HttpPost("Award/{offerId}")]
        [Authorize(Policy = "EditContract")]
        public async Task<ActionResult<ContractDetailDto>> Award(int offerId, [FromBody] AwardOfferRequestDto dto) => Ok(await _mediator.Send(new AwardContractOfferCommand(offerId, dto)));

        // ───────── الملاحق ─────────

        [HttpPost("Amendments/{contractId}")]
        [Authorize(Policy = "AddContractAmendment")]
        public async Task<ActionResult<ContractDetailDto>> AddAmendment(int contractId, [FromBody] ContractAmendmentRequestDto dto)
            => Ok(await _mediator.Send(new AddContractAmendmentCommand(contractId, dto)));

        // ───────── اللجنة الفنية والمالية ─────────

        [HttpPost("Committees/{contractId}")]
        [Authorize(Policy = "ManageContractCommittees")]
        public async Task<ActionResult<ContractDetailDto>> CreateCommittee(int contractId, [FromBody] CommitteeRequestDto dto)
            => Ok(await _mediator.Send(new CreateCommitteeCommand(contractId, dto)));

        /// <summary>تعديل اللجنة: يُستبدل أعضاؤها بالقائمة المرسلة</summary>
        [HttpPut("Committee/{committeeId}")]
        [Authorize(Policy = "ManageContractCommittees")]
        public async Task<ActionResult<ContractDetailDto>> UpdateCommittee(int committeeId, [FromBody] CommitteeRequestDto dto)
            => Ok(await _mediator.Send(new UpdateCommitteeCommand(committeeId, dto)));

        // ───────── المبالغ المحتجزة ─────────

        [HttpPost("Retention/{contractId}")]
        [Authorize(Policy = "RecordContractRetention")]
        public async Task<ActionResult<ContractDetailDto>> AddRetention(int contractId, [FromBody] RetentionRequestDto dto)
            => Ok(await _mediator.Send(new AddRetentionCommand(contractId, dto)));

        /// <summary>قرار الإفراج (1، نهائي) أو استمرار الاحتجاز (2) بالاعتماد الرسمي</summary>
        [HttpPost("RetentionDecision/{entryId}")]
        [Authorize(Policy = "RecordContractRetention")]
        public async Task<ActionResult<ContractDetailDto>> DecideRetention(int entryId, [FromBody] RetentionDecisionRequestDto dto)
            => Ok(await _mediator.Send(new DecideRetentionCommand(entryId, dto)));

        // ───────── تقييم المقاول ─────────

        [HttpPost("Evaluations/{contractId}")]
        [Authorize(Policy = "EvaluateContractor")]
        public async Task<ActionResult<ContractDetailDto>> CreateEvaluation(int contractId, [FromBody] ContractEvaluationRequestDto dto)
            => Ok(await _mediator.Send(new CreateContractEvaluationCommand(contractId, dto)));

        [HttpPut("Evaluation/{id}")]
        [Authorize(Policy = "EvaluateContractor")]
        public async Task<ActionResult<ContractDetailDto>> UpdateEvaluation(int id, [FromBody] ContractEvaluationRequestDto dto)
            => Ok(await _mediator.Send(new UpdateContractEvaluationCommand(id, dto)));

        /// <summary>حذف مسودة (لمن أعدّها)</summary>
        [HttpDelete("Evaluation/{id}")]
        [Authorize(Policy = "EvaluateContractor")]
        public async Task<ActionResult<ContractDetailDto>> DeleteEvaluation(int id) => Ok(await _mediator.Send(new DeleteContractEvaluationCommand(id)));

        [HttpPost("Evaluation/{id}/Submit")]
        [Authorize(Policy = "EvaluateContractor")]
        public async Task<ActionResult<ContractDetailDto>> SubmitEvaluation(int id, [FromBody] EvaluationStepRequestDto dto)
            => Ok(await _mediator.Send(new SubmitContractEvaluationCommand(id, dto)));

        /// <summary>مراجعة التقييم وحفظه في ملف المقاول (غير من أعدّه)</summary>
        [HttpPost("Evaluation/{id}/Review")]
        [Authorize(Policy = "ReviewContractorEvaluation")]
        public async Task<ActionResult<ContractDetailDto>> ReviewEvaluation(int id, [FromBody] EvaluationStepRequestDto dto)
            => Ok(await _mediator.Send(new ReviewContractEvaluationCommand(id, dto)));

        /// <summary>إعادة التقييم إلى مُعدّه لاستكماله (تشترط سبباً)</summary>
        [HttpPost("Evaluation/{id}/Return")]
        [Authorize(Policy = "ReviewContractorEvaluation")]
        public async Task<ActionResult<ContractDetailDto>> ReturnEvaluation(int id, [FromBody] EvaluationStepRequestDto dto)
            => Ok(await _mediator.Send(new ReturnContractEvaluationCommand(id, dto)));
    }

    /// <summary>المقاولون (بيانات الفرع) وملف المقاول. حدّها مقاولو فرعه، ولنظرة المؤسسة أي فرع</summary>
    [ApiController]
    [Route("api/Contractors")]
    [Authorize]
    public class ContractorsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ContractorsController(IMediator mediator) => _mediator = mediator;

        [HttpGet("GetAll")]
        [Authorize(Policy = "AnyContractorView")]
        public async Task<ActionResult<List<ContractorResponseDto>>> GetAll([FromQuery] int? branchId, [FromQuery] bool includeInactive = false)
            => Ok(await _mediator.Send(new GetContractorsQuery(branchId, includeInactive)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateContractor")]
        public async Task<ActionResult<ContractorResponseDto>> Create([FromBody] ContractorRequestDto dto) => Ok(await _mediator.Send(new CreateContractorCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditContractor")]
        public async Task<ActionResult<ContractorResponseDto>> Update(int id, [FromBody] ContractorRequestDto dto) => Ok(await _mediator.Send(new UpdateContractorCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteContractor")]
        public async Task<ActionResult<bool>> Delete(int id) => Ok(await _mediator.Send(new DeleteContractorCommand(id)));

        /// <summary>ملف المقاول: تقييماته المراجَعة على عقود مشاريع يراها المستخدم</summary>
        [HttpGet("Evaluations/{id}")]
        [Authorize(Policy = "ViewContractorEvaluations")]
        public async Task<ActionResult<List<ContractorEvaluationHistoryDto>>> Evaluations(int id) => Ok(await _mediator.Send(new GetContractorEvaluationsQuery(id)));
    }

    /// <summary>معايير تقييم المقاولين: يعرّفها كل فرع بأسمائه (إعدادات). لا يُحتسب منها مجموع ولا تصنيف</summary>
    [ApiController]
    [Route("api/EvaluationCriteria")]
    [Authorize]
    public class EvaluationCriteriaController : ControllerBase
    {
        private readonly IMediator _mediator;

        public EvaluationCriteriaController(IMediator mediator) => _mediator = mediator;

        [HttpGet("GetAll")]
        [Authorize(Policy = "AnyEvaluationCriterionView")]
        public async Task<ActionResult<List<EvaluationCriterionResponseDto>>> GetAll([FromQuery] int? branchId, [FromQuery] bool includeInactive = false)
            => Ok(await _mediator.Send(new GetEvaluationCriteriaQuery(branchId, includeInactive)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateEvaluationCriterion")]
        public async Task<ActionResult<EvaluationCriterionResponseDto>> Create([FromBody] EvaluationCriterionRequestDto dto) => Ok(await _mediator.Send(new CreateEvaluationCriterionCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditEvaluationCriterion")]
        public async Task<ActionResult<EvaluationCriterionResponseDto>> Update(int id, [FromBody] EvaluationCriterionRequestDto dto)
            => Ok(await _mediator.Send(new UpdateEvaluationCriterionCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteEvaluationCriterion")]
        public async Task<ActionResult<bool>> Delete(int id) => Ok(await _mediator.Send(new DeleteEvaluationCriterionCommand(id)));
    }
}
