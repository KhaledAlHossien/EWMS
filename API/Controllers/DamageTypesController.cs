using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Maintenance.DamageTypes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>أنواع الأعطال (جدول مساعد لطلبات الصيانة)</summary>
    [ApiController]
    [Route("api/DamageTypes")]
    [Authorize]
    public class DamageTypesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public DamageTypesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("GetAll")]
        [Authorize(Policy = "ViewMaintenanceLookups")]
        public async Task<ActionResult<List<DamageTypeResponseDto>>> GetAll()
            => Ok(await _mediator.Send(new GetAllDamageTypesQuery()));

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewMaintenanceLookups")]
        public async Task<ActionResult<DamageTypeResponseDto>> GetById(int id)
            => Ok(await _mediator.Send(new GetDamageTypeByIdQuery(id)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateMaintenanceLookup")]
        public async Task<ActionResult<DamageTypeResponseDto>> Create([FromBody] DamageTypeRequestDto dto)
            => Ok(await _mediator.Send(new CreateDamageTypeCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditMaintenanceLookup")]
        public async Task<ActionResult<DamageTypeResponseDto>> Update(int id, [FromBody] DamageTypeRequestDto dto)
            => Ok(await _mediator.Send(new UpdateDamageTypeCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteMaintenanceLookup")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteDamageTypeCommand(id));
            return Ok(new { message = "تم حذف نوع العطل بنجاح" });
        }
    }
}
