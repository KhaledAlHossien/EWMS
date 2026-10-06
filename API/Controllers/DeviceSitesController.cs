using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.DeviceInventory;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// تركيبات الأجهزة في المواقع. كلمة السر لا تُرسل في أي قائمة: تُقرأ من Password/{id} بصلاحية
    /// RevealDevicePasswords ويُسجَّل كل إظهار ونسخ في سجل التركيب.
    /// </summary>
    [ApiController]
    [Route("api/DeviceSites")]
    [Authorize]
    public class DeviceSitesController : ControllerBase
    {
        private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        private readonly IMediator _mediator;

        public DeviceSitesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>بحث مقسّم صفحات: q، governorateCode، siteId، deviceId، status (1 يعمل/2 معطّل/3 أُزيل)، page، pageSize (≤200)</summary>
        [HttpGet("Search")]
        [Authorize(Policy = "AnyDeviceView")]
        public async Task<ActionResult<PagedResultDto<DeviceSiteResponseDto>>> Search([FromQuery] InstallationFilterDto filter)
            => Ok(await _mediator.Send(new SearchInstallationsQuery(filter)));

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "AnyDeviceView")]
        public async Task<ActionResult<DeviceSiteResponseDto>> GetById(int id)
            => Ok(await _mediator.Send(new GetInstallationByIdQuery(id)));

        /// <summary>تركيبات أخرى بنفس الـ IP في الموقع (تنبيه النموذج)</summary>
        [HttpGet("IpInUse")]
        [Authorize(Policy = "AnyDeviceView")]
        public async Task<ActionResult<List<IpInUseDto>>> IpInUse([FromQuery] int siteId, [FromQuery] string? ip, [FromQuery] int? excludeId)
            => Ok(await _mediator.Send(new GetIpInUseQuery(siteId, ip ?? string.Empty, excludeId)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateDevice")]
        public async Task<ActionResult<DeviceSiteResponseDto>> Create([FromBody] InstallationRequestDto dto)
            => Ok(await _mediator.Send(new CreateInstallationCommand(dto)));

        /// <summary>pass فارغة = تبقى كلمة السر الحالية؛ rowVersion كما وصل</summary>
        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditDevice")]
        public async Task<ActionResult<DeviceSiteResponseDto>> Update(int id, [FromBody] InstallationRequestDto dto)
            => Ok(await _mediator.Send(new UpdateInstallationCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteDevice")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteInstallationCommand(id));
            return Ok(new { message = "تم حذف التركيب بنجاح" });
        }

        /// <summary>كلمة سر التركيب — copy=true للنسخ (يُسجَّل «نسخ») وإلا «إظهار»</summary>
        [HttpGet("Password/{id}")]
        [Authorize(Policy = "RevealDevicePasswords")]
        public async Task<ActionResult<DevicePasswordDto>> Password(int id, [FromQuery] bool copy = false)
        {
            Response.Headers.CacheControl = "no-store";
            return Ok(await _mediator.Send(new RevealDevicePasswordQuery(id, copy)));
        }

        /// <summary>«تحققت اليوم»: يسجّل تاريخ آخر تحقق من بيانات التركيب</summary>
        [HttpPut("Verify/{id}")]
        [Authorize(Policy = "EditDevice")]
        public async Task<ActionResult<DeviceSiteResponseDto>> Verify(int id)
            => Ok(await _mediator.Send(new VerifyInstallationCommand(id)));

        [HttpGet("History/{id}")]
        [Authorize(Policy = "AnyDeviceView")]
        public async Task<ActionResult<PagedResultDto<DeviceInventoryLogDto>>> History(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(await _mediator.Send(new GetDeviceInventoryHistoryQuery(DeviceInventoryEntity.Installation, id, page, pageSize)));

        /// <summary>تصدير نتائج البحث (نفس فلاتر Search) إلى Excel — بلا كلمات السر</summary>
        [HttpGet("Export")]
        [Authorize(Policy = "AnyDeviceView")]
        public async Task<IActionResult> Export([FromQuery] InstallationFilterDto filter)
            => File(await _mediator.Send(new ExportInstallationsQuery(filter)), Xlsx, $"device-installations-{DateTime.Now:yyyy-MM-dd}.xlsx");

        [HttpGet("ImportTemplate")]
        [Authorize(Policy = "CreateDevice")]
        public async Task<IActionResult> ImportTemplate()
            => File(await _mediator.Send(new GetImportTemplateQuery()), Xlsx, "device-installations-template.xlsx");

        /// <summary>استيراد ملف Excel: dryRun=true معاينة بلا حفظ، false يحفظ الأسطر الصالحة فقط</summary>
        [HttpPost("Import")]
        [Authorize(Policy = "CreateDevice")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<ActionResult<InstallationImportReportDto>> Import(IFormFile? file, [FromQuery] bool dryRun = true)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "اختر ملف Excel" });
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { message = "حجم الملف يتجاوز 5MB" });

            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;
            return Ok(await _mediator.Send(new ImportInstallationsCommand(stream, dryRun)));
        }
    }
}
