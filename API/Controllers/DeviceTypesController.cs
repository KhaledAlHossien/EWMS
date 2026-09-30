using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Maintenance.DeviceTypes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>أنواع الأجهزة (جدول مساعد لطلبات الصيانة)</summary>
    [ApiController]
    [Route("api/DeviceTypes")]
    [Authorize]
    public class DeviceTypesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public DeviceTypesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("GetAll")]
        [Authorize(Policy = "ViewMaintenanceLookups")]
        public async Task<ActionResult<List<DeviceTypeResponseDto>>> GetAll()
            => Ok(await _mediator.Send(new GetAllDeviceTypesQuery()));

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewMaintenanceLookups")]
        public async Task<ActionResult<DeviceTypeResponseDto>> GetById(int id)
            => Ok(await _mediator.Send(new GetDeviceTypeByIdQuery(id)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateMaintenanceLookup")]
        public async Task<ActionResult<DeviceTypeResponseDto>> Create([FromBody] DeviceTypeRequestDto dto)
            => Ok(await _mediator.Send(new CreateDeviceTypeCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditMaintenanceLookup")]
        public async Task<ActionResult<DeviceTypeResponseDto>> Update(int id, [FromBody] DeviceTypeRequestDto dto)
            => Ok(await _mediator.Send(new UpdateDeviceTypeCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteMaintenanceLookup")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteDeviceTypeCommand(id));
            return Ok(new { message = "تم حذف نوع الجهاز بنجاح" });
        }
    }
}
