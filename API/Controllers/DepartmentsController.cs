using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Department.Command.Create;
using Application.Features.Department.Command.Delete;
using Application.Features.Department.Command.Update;
using Application.Features.Department.Queries.GetAll;
using Application.Features.Department.Queries.GetById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/Department")]
    [Authorize]
    public class DepartmentsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public DepartmentsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("Create")]
        [Authorize(Policy = "ManageDepartments")]
        public async Task<ActionResult<DepartmentResponseDto>> Create([FromForm] CreateDepartmentRequestDto dto)
        {
            var result = await _mediator.Send(new CreateDepartmentCommand(dto));
            return Ok(result);
        }

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "ManageDepartments")]
        public async Task<ActionResult<DepartmentResponseDto>> Update(int id, [FromForm] UpdateDepartmentRequestDto dto)
        {
            var result = await _mediator.Send(new UpdateDepartmentCommand(id, dto));
            return Ok(result);
        }

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "ManageDepartments")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteDepartmentCommand(id));
            return Ok(new { message = "تم حذف القسم بنجاح" });
        }

        [HttpGet("Get/{id}")]
        public async Task<ActionResult<DepartmentResponseDto>> GetById(int id)
        {
            var result = await _mediator.Send(new GetDepartmentByIdQuery(id));
            return Ok(result);
        }

        [HttpGet("GetAll")]
        public async Task<ActionResult<List<DepartmentResponseDto>>> GetAll()
        {
            var result = await _mediator.Send(new GetAllDepartmentsQuery());
            return Ok(result);
        }
    }
}
