using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Devices.Commands.Create;
using Application.Features.Devices.Commands.Delete;
using Application.Features.Devices.Commands.Update;
using Application.Features.Devices.Queries.GetAll;
using Application.Features.Devices.Queries.GetById;
using Application.Features.Devices.Queries.GetMyAccess;
using Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
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

        [HttpPost("Create")]
        [Authorize(Policy = "CreateDevice")]
        public async Task<ActionResult<DeviceResponseDto>> Create([FromForm] CreateDeviceRequestDto dto)
        {
            return Ok(await _mediator.Send(new CreateDeviceCommand(dto)));
        }

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditDevice")]
        public async Task<ActionResult<DeviceResponseDto>> Update(int id, [FromForm] UpdateDeviceRequestDto dto)
        {
            return Ok(await _mediator.Send(new UpdateDeviceCommand(id, dto)));
        }

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteDevice")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteDeviceCommand(id));
            return Ok(new { message = "تم حذف الجهاز بنجاح" });
        }

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewDevices")]
        public async Task<ActionResult<DeviceResponseDto>> GetById(int id)
        {
            return Ok(await _mediator.Send(new GetDeviceByIdQuery(id)));
        }

        [HttpGet("GetAll")]
        [Authorize(Policy = "ViewDevices")]
        public async Task<ActionResult<List<DeviceResponseDto>>> GetAll()
        {
            return Ok(await _mediator.Send(new GetAllDevicesQuery()));
        }

        // صلاحيتي على توثيق الأجهزة (بدون سياسة: يُرجع false/false لمن لا يملكها)
        [HttpGet("MyAccess")]
        [Authorize(Policy = "ViewDevices")]
        public async Task<ActionResult<DeviceAccess>> MyAccess()
        {
            return Ok(await _mediator.Send(new GetMyDeviceAccessQuery()));
        }
    }
}
