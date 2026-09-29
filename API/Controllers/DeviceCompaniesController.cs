using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Maintenance.DeviceCompanies;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>الشركات المصنعة (جدول مساعد لطلبات الصيانة)</summary>
    [ApiController]
    [Route("api/DeviceCompanies")]
    [Authorize]
    public class DeviceCompaniesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public DeviceCompaniesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // القراءة لأي مستخدم مسجّل (قوائم منسدلة في نموذج الطلب والبحث)
        [HttpGet("GetAll")]
        public async Task<ActionResult<List<DeviceCompanyResponseDto>>> GetAll()
            => Ok(await _mediator.Send(new GetAllDeviceCompaniesQuery()));

        [HttpGet("Get/{id}")]
        public async Task<ActionResult<DeviceCompanyResponseDto>> GetById(int id)
            => Ok(await _mediator.Send(new GetDeviceCompanyByIdQuery(id)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateMaintenanceLookup")]
        public async Task<ActionResult<DeviceCompanyResponseDto>> Create([FromBody] DeviceCompanyRequestDto dto)
            => Ok(await _mediator.Send(new CreateDeviceCompanyCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditMaintenanceLookup")]
        public async Task<ActionResult<DeviceCompanyResponseDto>> Update(int id, [FromBody] DeviceCompanyRequestDto dto)
            => Ok(await _mediator.Send(new UpdateDeviceCompanyCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteMaintenanceLookup")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteDeviceCompanyCommand(id));
            return Ok(new { message = "تم حذف الشركة بنجاح" });
        }
    }
}
