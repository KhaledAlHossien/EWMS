using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.DeviceSites.Commands.Create;
using Application.Features.DeviceSites.Commands.Delete;
using Application.Features.DeviceSites.Commands.Update;
using Application.Features.DeviceSites.Queries.GetAll;
using Application.Features.DeviceSites.Queries.GetById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/DeviceSites")]
    [Authorize]
    public class DeviceSitesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public DeviceSitesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("Create")]
        [Authorize(Policy = "CreateDevice")]
        public async Task<ActionResult<DeviceSiteResponseDto>> Create([FromForm] CreateDeviceSiteRequestDto dto)
        {
            return Ok(await _mediator.Send(new CreateDeviceSiteCommand(dto)));
        }

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditDevice")]
        public async Task<ActionResult<DeviceSiteResponseDto>> Update(int id, [FromForm] UpdateDeviceSiteRequestDto dto)
        {
            return Ok(await _mediator.Send(new UpdateDeviceSiteCommand(id, dto)));
        }

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteDevice")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteDeviceSiteCommand(id));
            return Ok(new { message = "تم حذف الربط بنجاح" });
        }

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewDevices")]
        public async Task<ActionResult<DeviceSiteResponseDto>> GetById(int id)
        {
            return Ok(await _mediator.Send(new GetDeviceSiteByIdQuery(id)));
        }

        [HttpGet("GetAll")]
        [Authorize(Policy = "ViewDevices")]
        public async Task<ActionResult<List<DeviceSiteResponseDto>>> GetAll(
            [FromQuery] int? siteId, [FromQuery] int? deviceId)
        {
            return Ok(await _mediator.Send(new GetAllDeviceSitesQuery(siteId, deviceId)));
        }
    }
}
