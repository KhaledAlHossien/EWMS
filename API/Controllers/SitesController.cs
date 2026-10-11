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
    /// سجل المواقع المشترك بين الفروع (2026-10-11): القراءة لمن يدير المواقع أو يوثّق الأجهزة (AnySiteView)،
    /// والإضافة/التعديل/الحذف بصلاحيات المواقع. عدّادات التركيبات ضمن حدّ المستخدم.
    /// </summary>
    [ApiController]
    [Route("api/Sites")]
    [Authorize]
    public class SitesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SitesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>كل المواقع مع عدد تركيباتها (كلها، والتي تعمل)</summary>
        [HttpGet("GetAll")]
        [Authorize(Policy = "AnySiteView")]
        public async Task<ActionResult<List<SiteResponseDto>>> GetAll()
            => Ok(await _mediator.Send(new GetAllSitesQuery()));

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "AnySiteView")]
        public async Task<ActionResult<SiteResponseDto>> GetById(int id)
            => Ok(await _mediator.Send(new GetSiteByIdQuery(id)));

        /// <summary>مواقع على بُعد 200 متر من النقطة — تنبيه قبل إضافة موقع مكرر</summary>
        [HttpGet("Nearby")]
        [Authorize(Policy = "AnySiteView")]
        public async Task<ActionResult<List<NearbySiteDto>>> Nearby([FromQuery] double latitude, [FromQuery] double longitude, [FromQuery] int? excludeId)
            => Ok(await _mediator.Send(new GetNearbySitesQuery(latitude, longitude, excludeId)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateSite")]
        public async Task<ActionResult<SiteResponseDto>> Create([FromBody] SiteRequestDto dto)
            => Ok(await _mediator.Send(new CreateSiteCommand(dto)));

        /// <summary>يُرسل rowVersion كما وصل — يُرفض الحفظ إن عدّل غيرك الموقع منذ فتحته</summary>
        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditSite")]
        public async Task<ActionResult<SiteResponseDto>> Update(int id, [FromBody] SiteRequestDto dto)
            => Ok(await _mediator.Send(new UpdateSiteCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteSite")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteSiteCommand(id));
            return Ok(new { message = "تم حذف الموقع بنجاح" });
        }

        /// <summary>سجل تغييرات الموقع (الأحدث أولاً)</summary>
        [HttpGet("History/{id}")]
        [Authorize(Policy = "AnySiteView")]
        public async Task<ActionResult<PagedResultDto<DeviceInventoryLogDto>>> History(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(await _mediator.Send(new GetDeviceInventoryHistoryQuery(DeviceInventoryEntity.Site, id, page, pageSize)));
    }
}
