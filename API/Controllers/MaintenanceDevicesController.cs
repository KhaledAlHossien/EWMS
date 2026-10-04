using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Maintenance.MaintenanceDevices;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// أجهزة الصيانة (قطع فعلية برقم تسلسلي فريد). طلب الصيانة يُربط بجهاز من هنا،
    /// وسجل إصلاحات جهاز = MaintenanceRequests/GetAll?deviceMaintenanceId={id}
    /// </summary>
    [ApiController]
    [Route("api/MaintenanceDevices")]
    [Authorize]
    public class MaintenanceDevicesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MaintenanceDevicesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>بحث بفلاتر اختيارية: serialNumber و model (يبدأ بـ)، name (يحتوي)، deviceTypeId، deviceCompanyId، page، pageSize</summary>
        [HttpGet("GetAll")]
        [Authorize(Policy = "ViewMaintenanceDevices")]
        public async Task<ActionResult<PagedResultDto<DeviceMaintenanceResponseDto>>> GetAll(
            [FromQuery] DeviceMaintenanceFilterDto filter)
            => Ok(await _mediator.Send(new SearchDeviceMaintenancesQuery(filter)));

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewMaintenanceDevices")]
        public async Task<ActionResult<DeviceMaintenanceResponseDto>> GetById(int id)
            => Ok(await _mediator.Send(new GetDeviceMaintenanceByIdQuery(id)));

        /// <summary>الجهاز بالرقم التسلسلي (مطابقة تامة) — 404 إن لم يُسجَّل بعد، فتضيفه الواجهة ثم تقدّم الطلب</summary>
        [HttpGet("BySerial")]
        [Authorize(Policy = "ViewMaintenanceDevices")]
        public async Task<ActionResult<DeviceMaintenanceResponseDto>> BySerial([FromQuery] string serialNumber)
            => Ok(await _mediator.Send(new GetDeviceMaintenanceBySerialQuery(serialNumber ?? string.Empty)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateMaintenanceDevice")]
        public async Task<ActionResult<DeviceMaintenanceResponseDto>> Create([FromBody] DeviceMaintenanceRequestDto dto)
            => Ok(await _mediator.Send(new CreateDeviceMaintenanceCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditMaintenanceDevice")]
        public async Task<ActionResult<DeviceMaintenanceResponseDto>> Update(int id, [FromBody] DeviceMaintenanceRequestDto dto)
            => Ok(await _mediator.Send(new UpdateDeviceMaintenanceCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteMaintenanceDevice")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteDeviceMaintenanceCommand(id));
            return Ok(new { message = "تم حذف الجهاز بنجاح" });
        }
    }
}
