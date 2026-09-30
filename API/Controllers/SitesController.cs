using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Devices.Queries.GetBySite;
using Application.Features.Sites.Commands.Create;
using Application.Features.Sites.Commands.Delete;
using Application.Features.Sites.Commands.Update;
using Application.Features.Sites.Queries.GetAll;
using Application.Features.Sites.Queries.GetById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/Sites")]
    [Authorize]
    public class SitesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SitesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("Create")]
        [Authorize(Policy = "CreateDevice")]
        public async Task<ActionResult<SiteResponseDto>> Create([FromForm] CreateSiteRequestDto dto)
        {
            return Ok(await _mediator.Send(new CreateSiteCommand(dto)));
        }

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditDevice")]
        public async Task<ActionResult<SiteResponseDto>> Update(int id, [FromForm] UpdateSiteRequestDto dto)
        {
            return Ok(await _mediator.Send(new UpdateSiteCommand(id, dto)));
        }

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteDevice")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteSiteCommand(id));
            return Ok(new { message = "تم حذف الموقع بنجاح" });
        }

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewDevices")]
        public async Task<ActionResult<SiteResponseDto>> GetById(int id)
        {
            return Ok(await _mediator.Send(new GetSiteByIdQuery(id)));
        }

        [HttpGet("GetAll")]
        [Authorize(Policy = "ViewDevices")]
        public async Task<ActionResult<List<SiteResponseDto>>> GetAll()
        {
            return Ok(await _mediator.Send(new GetAllSitesQuery()));
        }

        [HttpGet("{id}/Devices")]
        [Authorize(Policy = "ViewDevices")]
        public async Task<ActionResult<List<DeviceResponseDto>>> GetDevices(int id)
        {
            return Ok(await _mediator.Send(new GetDevicesBySiteQuery(id)));
        }
    }
}
