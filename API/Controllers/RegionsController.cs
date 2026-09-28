using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Regions.Commands.Create;
using Application.Features.Regions.Commands.Delete;
using Application.Features.Regions.Commands.Update;
using Application.Features.Regions.Queries.GetAll;
using Application.Features.Regions.Queries.GetById;
using Application.Features.Regions.Queries.GetSites;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/Regions")]
    [Authorize]
    public class RegionsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public RegionsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("Create")]
        [Authorize(Policy = "ManageDevices")]
        public async Task<ActionResult<RegionResponseDto>> Create([FromForm] CreateRegionRequestDto dto)
        {
            return Ok(await _mediator.Send(new CreateRegionCommand(dto)));
        }

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "ManageDevices")]
        public async Task<ActionResult<RegionResponseDto>> Update(int id, [FromForm] UpdateRegionRequestDto dto)
        {
            return Ok(await _mediator.Send(new UpdateRegionCommand(id, dto)));
        }

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "ManageDevices")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteRegionCommand(id));
            return Ok(new { message = "تم حذف المنطقة بنجاح" });
        }

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewDevices")]
        public async Task<ActionResult<RegionResponseDto>> GetById(int id)
        {
            return Ok(await _mediator.Send(new GetRegionByIdQuery(id)));
        }

        [HttpGet("GetAll")]
        [Authorize(Policy = "ViewDevices")]
        public async Task<ActionResult<List<RegionResponseDto>>> GetAll()
        {
            return Ok(await _mediator.Send(new GetAllRegionsQuery()));
        }

        [HttpGet("{id}/Sites")]
        [Authorize(Policy = "ViewDevices")]
        public async Task<ActionResult<List<SiteResponseDto>>> GetSites(int id)
        {
            return Ok(await _mediator.Send(new GetSitesByRegionQuery(id)));
        }
    }
}
