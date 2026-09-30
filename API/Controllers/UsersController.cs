using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Users.Commands.Create;
using Application.Features.Users.Commands.Delete;
using Application.Features.Users.Commands.Update;
using Application.Features.Users.Queries.GetAll;
using Application.Features.Users.Queries.GetByBranch;
using Application.Features.Users.Queries.GetByDepartment;
using Application.Features.Users.Queries.GetByOffice;
using Application.Features.Users.Queries.GetById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/Users")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IMediator _mediator;

        public UsersController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("Create")]
        [Authorize(Policy = "CreateUser")]
        public async Task<ActionResult<UserResponseDto>> Create([FromForm] CreateUserRequestDto dto)
        {
            return Ok(await _mediator.Send(new CreateUserCommand(dto)));
        }

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditUser")]
        public async Task<ActionResult<UserResponseDto>> Update(int id, [FromForm] UpdateUserRequestDto dto)
        {
            return Ok(await _mediator.Send(new UpdateUserCommand(id, dto)));
        }

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteUser")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteUserCommand(id));
            return Ok(new { message = "تم حذف المستخدم بنجاح" });
        }

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewUsers")]
        public async Task<ActionResult<UserResponseDto>> GetById(int id)
        {
            return Ok(await _mediator.Send(new GetUserByIdQuery(id)));
        }

        [HttpGet("GetAll")]
        [Authorize(Policy = "ViewUsers")]
        public async Task<ActionResult<List<UserResponseDto>>> GetAll()
        {
            return Ok(await _mediator.Send(new GetAllUsersQuery()));
        }

        [HttpGet("Branch/{branchId}")]
        [Authorize(Policy = "ViewUsers")]
        public async Task<ActionResult<List<UserResponseDto>>> GetByBranch(int branchId)
        {
            return Ok(await _mediator.Send(new GetUsersByBranchQuery(branchId)));
        }

        [HttpGet("Department/{departmentId}")]
        [Authorize(Policy = "ViewUsers")]
        public async Task<ActionResult<List<UserResponseDto>>> GetByDepartment(int departmentId)
        {
            return Ok(await _mediator.Send(new GetUsersByDepartmentQuery(departmentId)));
        }

        [HttpGet("Office/{officeId}")]
        [Authorize(Policy = "ViewUsers")]
        public async Task<ActionResult<List<UserResponseDto>>> GetByOffice(int officeId)
        {
            return Ok(await _mediator.Send(new GetUsersByOfficeQuery(officeId)));
        }
    }
}
