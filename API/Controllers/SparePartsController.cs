using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Maintenance.SpareParts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// مخزون قطع الغيار (مخزون لكل قسم) والصرف على طلبات الصيانة. الصلاحية تفتح العملية،
    /// وحدّها (قطع قسمي / طلبات نطاقي) يفحصه الـ handler.
    /// </summary>
    [ApiController]
    [Route("api/SpareParts")]
    [Authorize]
    public class SparePartsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SparePartsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>الأقسام التي يدير المستخدم مخزونها (قسمه، ومدير النظام كلها)</summary>
        [HttpGet("Departments")]
        [Authorize(Policy = "ViewSpareParts")]
        public async Task<ActionResult<List<NamedRefDto>>> Departments()
            => Ok(await _mediator.Send(new GetSparePartDepartmentsQuery()));

        /// <summary>بحث: search (يحتوي في الاسم أو يبدأ برقم القطعة)، departmentId، deviceTypeId، lowStock، page، pageSize</summary>
        [HttpGet("GetAll")]
        [Authorize(Policy = "ViewSpareParts")]
        public async Task<ActionResult<PagedResultDto<SparePartResponseDto>>> GetAll([FromQuery] SparePartFilterDto filter)
            => Ok(await _mediator.Send(new SearchSparePartsQuery(filter)));

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewSpareParts")]
        public async Task<ActionResult<SparePartResponseDto>> Get(int id)
            => Ok(await _mediator.Send(new GetSparePartByIdQuery(id)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateSparePart")]
        public async Task<ActionResult<SparePartResponseDto>> Create([FromBody] SparePartRequestDto dto)
            => Ok(await _mediator.Send(new CreateSparePartCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditSparePart")]
        public async Task<ActionResult<SparePartResponseDto>> Update(int id, [FromBody] SparePartRequestDto dto)
            => Ok(await _mediator.Send(new UpdateSparePartCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteSparePart")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteSparePartCommand(id));
            return Ok(new { message = "تم حذف القطعة بنجاح" });
        }

        /// <summary>إدخال: {quantity, unitCost, date?, source}</summary>
        [HttpPost("Receive/{id}")]
        [Authorize(Policy = "ReceiveSpareParts")]
        public async Task<ActionResult<SparePartResponseDto>> Receive(int id, [FromBody] ReceiveSparePartDto dto)
            => Ok(await _mediator.Send(new ReceiveSparePartCommand(id, dto)));

        /// <summary>تسوية: {delta, reason} — موجب زيادة، سالب نقص أو تالف</summary>
        [HttpPost("Adjust/{id}")]
        [Authorize(Policy = "AdjustSparePartStock")]
        public async Task<ActionResult<SparePartResponseDto>> Adjust(int id, [FromBody] AdjustSparePartDto dto)
            => Ok(await _mediator.Send(new AdjustSparePartCommand(id, dto)));

        [HttpGet("Movements/{id}")]
        [Authorize(Policy = "ViewSpareParts")]
        public async Task<ActionResult<PagedResultDto<SparePartMovementDto>>> Movements(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(await _mediator.Send(new GetSparePartMovementsQuery(id, page, pageSize)));

        /// <summary>التقارير: from/to (افتراضياً الشهر الحالي)، departmentId لمن يدير كل الأقسام</summary>
        [HttpGet("Report")]
        [Authorize(Policy = "ViewSparePartReports")]
        public async Task<ActionResult<SparePartReportDto>> Report([FromQuery] int? departmentId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
            => Ok(await _mediator.Send(new GetSparePartReportQuery(departmentId, from, to)));

        // ════════ قطع طلب الصيانة ════════

        /// <summary>قطع الطلب وتكلفته وتكلفة الجهاز على مدى عمره — بنطاق عرض الطلبات</summary>
        [HttpGet("Request/{requestId}")]
        [Authorize(Policy = "ViewMaintenanceRequests")]
        public async Task<ActionResult<RequestPartsDto>> RequestParts(int requestId)
            => Ok(await _mediator.Send(new GetRequestPartsQuery(requestId)));

        /// <summary>قطع مخزون قسم الطلب المتوفرة للصرف (المتوافقة مع الجهاز أولاً)</summary>
        [HttpGet("Request/{requestId}/Available")]
        [Authorize(Policy = "IssueSparePart")]
        public async Task<ActionResult<List<SparePartResponseDto>>> Available(int requestId, [FromQuery] string? search)
            => Ok(await _mediator.Send(new GetAvailablePartsForRequestQuery(requestId, search)));

        /// <summary>صرف: {sparePartId, quantity}</summary>
        [HttpPost("Request/{requestId}/Issue")]
        [Authorize(Policy = "IssueSparePart")]
        public async Task<ActionResult<RequestPartsDto>> Issue(int requestId, [FromBody] IssueSparePartDto dto)
            => Ok(await _mediator.Send(new IssueSparePartCommand(requestId, dto)));

        /// <summary>إزالة قطعة من الطلب وإعادتها للمخزون</summary>
        [HttpDelete("RequestPart/{requestPartId}")]
        [Authorize(Policy = "IssueSparePart")]
        public async Task<ActionResult<RequestPartsDto>> Return(int requestPartId)
            => Ok(await _mediator.Send(new ReturnSparePartCommand(requestPartId)));
    }
}
