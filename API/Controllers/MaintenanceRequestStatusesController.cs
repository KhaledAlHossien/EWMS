using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Maintenance.RequestStatuses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>حالات طلب الصيانة (جدول مساعد)</summary>
    [ApiController]
    [Route("api/MaintenanceRequestStatuses")]
    [Authorize]
    public class MaintenanceRequestStatusesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MaintenanceRequestStatusesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("GetAll")]
        [Authorize(Policy = "ViewMaintenanceLookups")]
        public async Task<ActionResult<List<MaintenanceRequestStatusResponseDto>>> GetAll()
            => Ok(await _mediator.Send(new GetAllMaintenanceRequestStatusesQuery()));

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewMaintenanceLookups")]
        public async Task<ActionResult<MaintenanceRequestStatusResponseDto>> GetById(int id)
            => Ok(await _mediator.Send(new GetMaintenanceRequestStatusByIdQuery(id)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateMaintenanceLookup")]
        public async Task<ActionResult<MaintenanceRequestStatusResponseDto>> Create([FromBody] MaintenanceRequestStatusRequestDto dto)
            => Ok(await _mediator.Send(new CreateMaintenanceRequestStatusCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditMaintenanceLookup")]
        public async Task<ActionResult<MaintenanceRequestStatusResponseDto>> Update(int id, [FromBody] MaintenanceRequestStatusRequestDto dto)
            => Ok(await _mediator.Send(new UpdateMaintenanceRequestStatusCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteMaintenanceLookup")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteMaintenanceRequestStatusCommand(id));
            return Ok(new { message = "تم حذف الحالة بنجاح" });
        }
    }
}
