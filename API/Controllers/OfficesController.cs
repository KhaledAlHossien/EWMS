using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Offices.Commands.Create;
using Application.Features.Offices.Commands.Delete;
using Application.Features.Offices.Commands.Update;
using Application.Features.Offices.Queries.GetAll;
using Application.Features.Offices.Queries.GetById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/Offices")]
    [Authorize]
    public class OfficesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public OfficesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("Create")]
        [Authorize(Policy = "CreateOffice")]
        public async Task<ActionResult<OfficeResponseDto>> Create([FromForm] CreateOfficeRequestDto dto)
        {
            return Ok(await _mediator.Send(new CreateOfficeCommand(dto)));
        }

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditOffice")]
        public async Task<ActionResult<OfficeResponseDto>> Update(int id, [FromForm] UpdateOfficeRequestDto dto)
        {
            return Ok(await _mediator.Send(new UpdateOfficeCommand(id, dto)));
        }

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteOffice")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteOfficeCommand(id));
            return Ok(new { message = "تم حذف المكتب بنجاح" });
        }

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewOffices")]
        public async Task<ActionResult<OfficeResponseDto>> GetById(int id)
        {
            return Ok(await _mediator.Send(new GetOfficeByIdQuery(id)));
        }

        [HttpGet("GetAll")]
        [Authorize(Policy = "ViewOffices")]
        public async Task<ActionResult<List<OfficeResponseDto>>> GetAll()
        {
            return Ok(await _mediator.Send(new GetAllOfficesQuery()));
        }
    }
}
