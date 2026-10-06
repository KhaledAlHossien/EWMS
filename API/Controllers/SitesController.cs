using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.DeviceInventory;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>مواقع توثيق الأجهزة — القراءة لمن يملك أي صلاحية أجهزة (AnyDeviceView)</summary>
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
        [Authorize(Policy = "AnyDeviceView")]
        public async Task<ActionResult<List<SiteResponseDto>>> GetAll()
            => Ok(await _mediator.Send(new GetAllSitesQuery()));

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "AnyDeviceView")]
        public async Task<ActionResult<SiteResponseDto>> GetById(int id)
            => Ok(await _mediator.Send(new GetSiteByIdQuery(id)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateDevice")]
        public async Task<ActionResult<SiteResponseDto>> Create([FromBody] SiteRequestDto dto)
            => Ok(await _mediator.Send(new CreateSiteCommand(dto)));

        /// <summary>يُرسل rowVersion كما وصل — يُرفض الحفظ إن عدّل غيرك الموقع منذ فتحته</summary>
        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditDevice")]
        public async Task<ActionResult<SiteResponseDto>> Update(int id, [FromBody] SiteRequestDto dto)
            => Ok(await _mediator.Send(new UpdateSiteCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteDevice")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteSiteCommand(id));
            return Ok(new { message = "تم حذف الموقع بنجاح" });
        }

        /// <summary>سجل تغييرات الموقع (الأحدث أولاً)</summary>
        [HttpGet("History/{id}")]
        [Authorize(Policy = "AnyDeviceView")]
        public async Task<ActionResult<PagedResultDto<DeviceInventoryLogDto>>> History(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(await _mediator.Send(new GetDeviceInventoryHistoryQuery(DeviceInventoryEntity.Site, id, page, pageSize)));
    }
}
