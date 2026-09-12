using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Permissions.Queries.GetAll;
using Application.Features.Roles.Commands.Create;
using Application.Features.Roles.Commands.Delete;
using Application.Features.Roles.Commands.Update;
using Application.Features.Roles.Queries.GetAll;
using Application.Features.Roles.Queries.GetById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/Roles")]
    [Authorize]
    public class RolesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public RolesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("Create")]
        [Authorize(Policy = "ManageRoles")]
        public async Task<ActionResult<RoleResponseDto>> Create([FromForm] CreateRoleRequestDto dto)
        {
            return Ok(await _mediator.Send(new CreateRoleCommand(dto)));
        }

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "ManageRoles")]
        public async Task<ActionResult<RoleResponseDto>> Update(int id, [FromForm] UpdateRoleRequestDto dto)
        {
            return Ok(await _mediator.Send(new UpdateRoleCommand(id, dto)));
        }

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "ManageRoles")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteRoleCommand(id));
            return Ok(new { message = "تم حذف الدور بنجاح" });
        }

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ManageRoles")]
        public async Task<ActionResult<RoleResponseDto>> GetById(int id)
        {
            return Ok(await _mediator.Send(new GetRoleByIdQuery(id)));
        }

        [HttpGet("GetAll")]
        [Authorize(Policy = "ManageRoles")]
        public async Task<ActionResult<List<RoleResponseDto>>> GetAll()
        {
            return Ok(await _mediator.Send(new GetAllRolesQuery()));
        }

        [HttpGet("Permissions")]
        [Authorize(Policy = "ManageRoles")]
        public async Task<ActionResult<List<PermissionResponseDto>>> GetPermissions()
        {
            return Ok(await _mediator.Send(new GetAllPermissionsQuery()));
        }
    }
}
