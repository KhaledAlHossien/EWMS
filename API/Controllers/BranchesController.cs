using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Branches.Commands.Create;
using Application.Features.Branches.Commands.Delete;
using Application.Features.Branches.Commands.Update;
using Application.Features.Branches.Queries.GetAll;
using Application.Features.Branches.Queries.GetById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/Branches")]
    [Authorize]
    public class BranchesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public BranchesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("Create")]
        [Authorize(Policy = "ManageBranches")]
        public async Task<ActionResult<BranchResponseDto>> Create([FromForm] CreateBranchRequestDto dto)
        {
            return Ok(await _mediator.Send(new CreateBranchCommand(dto)));
        }

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "ManageBranches")]
        public async Task<ActionResult<BranchResponseDto>> Update(int id, [FromForm] UpdateBranchRequestDto dto)
        {
            return Ok(await _mediator.Send(new UpdateBranchCommand(id, dto)));
        }

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "ManageBranches")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteBranchCommand(id));
            return Ok(new { message = "تم حذف الفرع بنجاح" });
        }

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ManageBranches")]
        public async Task<ActionResult<BranchResponseDto>> GetById(int id)
        {
            return Ok(await _mediator.Send(new GetBranchByIdQuery(id)));
        }

        [HttpGet("GetAll")]
        [Authorize(Policy = "ManageBranches")]
        public async Task<ActionResult<List<BranchResponseDto>>> GetAll()
        {
            return Ok(await _mediator.Send(new GetAllBranchesQuery()));
        }
    }
}
