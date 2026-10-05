using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Maintenance.Requests;
using Application.Features.Maintenance.Transfers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// طلبات الصيانة. الصلاحية تحدد العملية، والنطاق (صاحب الطلب / رئيس قسمه / السوبر ادمن)
    /// يُفحص داخل المعالجات — راجع Application/Features/Maintenance/MaintenanceRules.
    /// </summary>
    [ApiController]
    [Route("api/MaintenanceRequests")]
    [Authorize]
    public class MaintenanceRequestsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MaintenanceRequestsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// بحث بفلاتر اختيارية: serialNumber و model (يبدأ بـ)، clientName (يحتوي)،
        /// deviceCompanyId، technicianId، deviceTypeId، damageTypeId، statusId، page، pageSize
        /// </summary>
        [HttpGet("GetAll")]
        [Authorize(Policy = "ViewMaintenanceRequests")]
        public async Task<ActionResult<PagedResultDto<MaintenanceRequestResponseDto>>> GetAll(
            [FromQuery] MaintenanceRequestFilterDto filter)
            => Ok(await _mediator.Send(new SearchMaintenanceRequestsQuery(filter)));

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewMaintenanceRequests")]
        public async Task<ActionResult<MaintenanceRequestResponseDto>> GetById(int id)
            => Ok(await _mediator.Send(new GetMaintenanceRequestByIdQuery(id)));

        // قائمة الفنيين لفلتر البحث
        [HttpGet("Technicians")]
        [Authorize(Policy = "ViewMaintenanceRequests")]
        public async Task<ActionResult<List<TechnicianOptionDto>>> Technicians()
            => Ok(await _mediator.Send(new GetMaintenanceTechniciansQuery()));

        // سجل الطلب: من فعل ماذا ومتى
        [HttpGet("Activities/{id}")]
        [Authorize(Policy = "ViewMaintenanceRequests")]
        public async Task<ActionResult<List<MaintenanceActivityDto>>> Activities(int id)
            => Ok(await _mediator.Send(new GetMaintenanceRequestActivitiesQuery(id)));

        // بيانات الطباعة (إيصال الاستلام / ورقة التسليم بتوقيع صاحب SignMaintenanceReceipt في القسم)
        [HttpGet("Print/{id}")]
        [Authorize(Policy = "ViewMaintenanceRequests")]
        public async Task<ActionResult<MaintenancePrintDto>> Print(int id)
            => Ok(await _mediator.Send(new GetMaintenanceRequestPrintQuery(id)));

        // إحصائيات الصيانة للسجلات التي أراها
        [HttpGet("Stats")]
        [Authorize(Policy = "ViewMaintenanceStats")]
        public async Task<ActionResult<MaintenanceStatsDto>> Stats()
            => Ok(await _mediator.Send(new GetMaintenanceStatsQuery()));

        // الموظفون المتاحون لنقل طلب/مهمة إليهم (موظفو قسمي لمن يملك صلاحية النقل، وفارغة لغيره)
        [HttpGet("Assignees")]
        [Authorize(Policy = "AnyMaintenanceAssign")]
        public async Task<ActionResult<List<TechnicianOptionDto>>> Assignees([FromQuery] int? departmentId)
            => Ok(await _mediator.Send(new GetMaintenanceAssigneesQuery(departmentId)));

        [HttpPut("Status/{id}")]
        [Authorize(Policy = "ChangeMaintenanceStatus")]
        public async Task<ActionResult<MaintenanceRequestResponseDto>> ChangeStatus(int id, [FromBody] ChangeMaintenanceStatusDto dto)
            => Ok(await _mediator.Send(new ChangeMaintenanceRequestStatusCommand(id, dto.StatusId)));

        [HttpPut("Assign/{id}")]
        [Authorize(Policy = "AssignMaintenanceRequest")]
        public async Task<ActionResult<MaintenanceRequestResponseDto>> Assign(int id, [FromBody] AssignMaintenanceDto dto)
            => Ok(await _mediator.Send(new AssignMaintenanceRequestCommand(id, dto.UserId)));

        // ========== طلبات التحويل (الفني يطلب، ورئيس القسم يقرّر) ==========
        [HttpPost("TransferRequest/{id}")]
        [Authorize(Policy = "RequestMaintenanceTransfer")]
        public async Task<ActionResult<MaintenanceTransferDto>> RequestTransfer(int id, [FromBody] RequestMaintenanceTransferDto dto)
            => Ok(await _mediator.Send(new RequestMaintenanceTransferCommand(id, dto)));

        [HttpPut("Transfer/{transferId}/Decide")]
        [Authorize(Policy = "AssignMaintenanceRequest")]
        public async Task<ActionResult<MaintenanceTransferDto>> DecideTransfer(int transferId, [FromBody] DecideMaintenanceTransferDto dto)
            => Ok(await _mediator.Send(new DecideMaintenanceTransferCommand(transferId, dto)));

        /// <summary>طلب التحويل المعلّق لطلب (204 إن لم يوجد)</summary>
        [HttpGet("Transfer/{requestId}")]
        [Authorize(Policy = "ViewMaintenanceRequests")]
        public async Task<ActionResult<MaintenanceTransferDto?>> PendingTransfer(int requestId)
            => Ok(await _mediator.Send(new GetPendingTransferQuery(requestId)));

        [HttpGet("Transfers/Pending")]
        [Authorize(Policy = "AssignMaintenanceRequest")]
        public async Task<ActionResult<List<MaintenanceTransferDto>>> PendingTransfers()
            => Ok(await _mediator.Send(new GetPendingTransfersForMeQuery()));

        /// <summary>زملاء قسمي (لاقتراح من يُحوَّل إليه الطلب)</summary>
        [HttpGet("Transfers/Colleagues")]
        [Authorize(Policy = "RequestMaintenanceTransfer")]
        public async Task<ActionResult<List<TechnicianOptionDto>>> TransferColleagues()
            => Ok(await _mediator.Send(new GetTransferColleaguesQuery()));

        // ========== العميل: موظف (مطابقة تامة) أو خارجي ==========
        [HttpGet("ClientLookup")]
        [Authorize(Policy = "AnyMaintenanceRequestWrite")]
        public async Task<ActionResult<List<MaintenanceClientDto>>> ClientLookup([FromQuery] string query)
            => Ok(await _mediator.Send(new FindMaintenanceClientQuery(query ?? string.Empty)));

        /// <summary>«أجهزتي في الصيانة»: طلبات أنا عميلها</summary>
        [HttpGet("Mine")]
        [Authorize(Policy = "ViewMyMaintenanceRequests")]
        public async Task<ActionResult<List<MyMaintenanceRequestDto>>> Mine()
            => Ok(await _mediator.Send(new GetMyMaintenanceRequestsQuery()));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateMaintenanceRequest")]
        public async Task<ActionResult<MaintenanceRequestResponseDto>> Create([FromBody] SaveMaintenanceRequestDto dto)
            => Ok(await _mediator.Send(new CreateMaintenanceRequestCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditMaintenanceRequest")]
        public async Task<ActionResult<MaintenanceRequestResponseDto>> Update(int id, [FromBody] SaveMaintenanceRequestDto dto)
            => Ok(await _mediator.Send(new UpdateMaintenanceRequestCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteMaintenanceRequest")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteMaintenanceRequestCommand(id));
            return Ok(new { message = "تم حذف طلب الصيانة بنجاح" });
        }
    }
}
