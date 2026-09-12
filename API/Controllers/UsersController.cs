using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Users.Commands.Create;
using Application.Features.Users.Commands.Delete;
using Application.Features.Users.Commands.Update;
using Application.Features.Users.Queries.GetAll;
using Application.Features.Users.Queries.GetByBranch;
using Application.Features.Users.Queries.GetByDepartment;
using Application.Features.Users.Queries.GetById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/Users")]
    [Authorize(Policy = "ManageUsers")]
    public class UsersController : ControllerBase
    {
        private readonly IMediator _mediator;

        public UsersController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("Create")]
        public async Task<ActionResult<UserResponseDto>> Create([FromForm] CreateUserRequestDto dto)
        {
            return Ok(await _mediator.Send(new CreateUserCommand(dto)));
        }

        [HttpPut("Update/{id}")]
        public async Task<ActionResult<UserResponseDto>> Update(int id, [FromForm] UpdateUserRequestDto dto)
        {
            return Ok(await _mediator.Send(new UpdateUserCommand(id, dto)));
        }

        [HttpDelete("Delete/{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteUserCommand(id));
            return Ok(new { message = "تم حذف المستخدم بنجاح" });
        }

        [HttpGet("Get/{id}")]
        public async Task<ActionResult<UserResponseDto>> GetById(int id)
        {
            return Ok(await _mediator.Send(new GetUserByIdQuery(id)));
        }

        [HttpGet("GetAll")]
        public async Task<ActionResult<List<UserResponseDto>>> GetAll()
        {
            return Ok(await _mediator.Send(new GetAllUsersQuery()));
        }

        [HttpGet("Branch/{branchId}")]
        public async Task<ActionResult<List<UserResponseDto>>> GetByBranch(int branchId)
        {
            return Ok(await _mediator.Send(new GetUsersByBranchQuery(branchId)));
        }

        [HttpGet("Department/{departmentId}")]
        public async Task<ActionResult<List<UserResponseDto>>> GetByDepartment(int departmentId)
        {
            return Ok(await _mediator.Send(new GetUsersByDepartmentQuery(departmentId)));
        }
    }
}
