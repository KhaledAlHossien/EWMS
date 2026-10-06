using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.DeviceInventory;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>كتالوج الأجهزة (نوع/موديل قابل للتركيب عدة مرات — الاسم + الموديل فريدان معاً)</summary>
    [ApiController]
    [Route("api/Devices")]
    [Authorize]
    public class DevicesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public DevicesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>كل الأجهزة مع عدد تركيبات كل منها</summary>
        [HttpGet("GetAll")]
        [Authorize(Policy = "AnyDeviceView")]
        public async Task<ActionResult<List<DeviceResponseDto>>> GetAll()
            => Ok(await _mediator.Send(new GetAllDevicesQuery()));

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "AnyDeviceView")]
        public async Task<ActionResult<DeviceResponseDto>> GetById(int id)
            => Ok(await _mediator.Send(new GetDeviceByIdQuery(id)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateDevice")]
        public async Task<ActionResult<DeviceResponseDto>> Create([FromBody] DeviceRequestDto dto)
            => Ok(await _mediator.Send(new CreateDeviceCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditDevice")]
        public async Task<ActionResult<DeviceResponseDto>> Update(int id, [FromBody] DeviceRequestDto dto)
            => Ok(await _mediator.Send(new UpdateDeviceCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteDevice")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteDeviceCommand(id));
            return Ok(new { message = "تم حذف الجهاز بنجاح" });
        }

        [HttpGet("History/{id}")]
        [Authorize(Policy = "AnyDeviceView")]
        public async Task<ActionResult<PagedResultDto<DeviceInventoryLogDto>>> History(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(await _mediator.Send(new GetDeviceInventoryHistoryQuery(DeviceInventoryEntity.Device, id, page, pageSize)));
    }
}
