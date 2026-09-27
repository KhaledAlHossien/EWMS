using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.VacationTypes.Commands.Create;
using Application.Features.VacationTypes.Commands.Delete;
using Application.Features.VacationTypes.Commands.Update;
using Application.Features.VacationTypes.Queries.GetAll;
using Application.Features.VacationTypes.Queries.GetById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/VacationTypes")]
    [Authorize]
    public class VacationTypesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public VacationTypesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // ========== عرض الكل ==========
        [HttpGet("GetAll")]
        public async Task<ActionResult<List<VacationTypeResponseDto>>> GetAll()
            => Ok(await _mediator.Send(new GetAllVacationTypesQuery()));

        // ========== عرض نوع واحد ==========
        [HttpGet("Get/{id}")]
        public async Task<ActionResult<VacationTypeResponseDto>> GetById(int id)
            => Ok(await _mediator.Send(new GetVacationTypeByIdQuery(id)));

        // ========== إنشاء ==========
        [HttpPost("Create")]
        [Authorize(Policy = "ManageVacationTypes")]
        public async Task<ActionResult<VacationTypeResponseDto>> Create(
            [FromBody] CreateVacationTypeRequestDto dto)
            => Ok(await _mediator.Send(new CreateVacationTypeCommand(dto)));

        // ========== تعديل ==========
        [HttpPut("Update/{id}")]
        [Authorize(Policy = "ManageVacationTypes")]
        public async Task<ActionResult<VacationTypeResponseDto>> Update(
            int id, [FromBody] UpdateVacationTypeRequestDto dto)
            => Ok(await _mediator.Send(new UpdateVacationTypeCommand(id, dto)));

        // ========== حذف ==========
        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "ManageVacationTypes")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteVacationTypeCommand(id));
            return Ok(new { message = "تم حذف نوع الإجازة بنجاح" });
        }
    }
}