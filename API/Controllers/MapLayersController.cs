using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.MapLayers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// طبقات خرائط الفروع وحقولها (1.2.0): الطبقة ملك الفرع — الحد طبقات فرعي (والكل لنظرة المؤسسة) داخل الـ handler.
    /// القراءة لمن يديرها أو يعمل على سجلاتها (AnyMapLayerRead).
    /// </summary>
    [ApiController]
    [Route("api/MapLayers")]
    [Authorize]
    public class MapLayersController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MapLayersController(IMediator mediator) => _mediator = mediator;

        /// <summary>branchId لنظرة المؤسسة فقط (فارغ = كل الفروع)</summary>
        [HttpGet("GetAll")]
        [Authorize(Policy = "AnyMapLayerRead")]
        public async Task<ActionResult<List<MapLayerDefinitionDto>>> GetAll([FromQuery] int? branchId, [FromQuery] bool includeInactive = true)
            => Ok(await _mediator.Send(new GetMapLayersQuery(branchId, includeInactive)));

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "AnyMapLayerRead")]
        public async Task<ActionResult<MapLayerDefinitionDto>> Get(int id) => Ok(await _mediator.Send(new GetMapLayerQuery(id)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateMapLayer")]
        public async Task<ActionResult<MapLayerDefinitionDto>> Create([FromBody] MapLayerRequestDto dto)
            => Ok(await _mediator.Send(new CreateMapLayerCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditMapLayer")]
        public async Task<ActionResult<MapLayerDefinitionDto>> Update(int id, [FromBody] MapLayerRequestDto dto)
            => Ok(await _mediator.Send(new UpdateMapLayerCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteMapLayer")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteMapLayerCommand(id));
            return Ok(new { message = "تم حذف الطبقة" });
        }

        /// <summary>عدد السجلات التي لها قيمة في الحقل — قبل حذفه</summary>
        [HttpGet("FieldUsage/{fieldId}")]
        [Authorize(Policy = "EditMapLayer")]
        public async Task<ActionResult<FieldUsageDto>> FieldUsage(int fieldId) => Ok(await _mediator.Send(new GetFieldUsageQuery(fieldId)));

        /// <summary>حذف حقل: بلا قيم يُحذف؛ له قيم: purge=false يخفيه (تبقى القيم)، purge=true يحذفه مع قيمه نهائياً</summary>
        [HttpDelete("Field/{fieldId}")]
        [Authorize(Policy = "EditMapLayer")]
        public async Task<ActionResult<MapLayerDefinitionDto>> DeleteField(int fieldId, [FromQuery] bool purge = false)
            => Ok(await _mediator.Send(new DeleteMapLayerFieldCommand(fieldId, purge)));

        [HttpPut("Field/{fieldId}/Restore")]
        [Authorize(Policy = "EditMapLayer")]
        public async Task<ActionResult<MapLayerDefinitionDto>> RestoreField(int fieldId) => Ok(await _mediator.Send(new RestoreMapLayerFieldCommand(fieldId)));
    }

    /// <summary>سجلات الطبقات على المواقع: الحد قسمي، أو فرعي مع ViewBranchMapEntries، أو الكل لنظرة المؤسسة</summary>
    [ApiController]
    [Route("api/MapEntries")]
    [Authorize]
    public class MapEntriesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MapEntriesController(IMediator mediator) => _mediator = mediator;

        /// <summary>سجلات الموقع التي أراها، والطبقات التي أستطيع الإضافة إليها بحقولها</summary>
        [HttpGet("Site/{siteId}")]
        [Authorize(Policy = "AnyMapEntryView")]
        public async Task<ActionResult<SiteEntriesDto>> Site(int siteId) => Ok(await _mediator.Send(new GetSiteEntriesQuery(siteId)));

        [HttpPost("Create")]
        [Authorize(Policy = "CreateMapEntry")]
        public async Task<ActionResult<MapEntryDto>> Create([FromBody] MapEntryRequestDto dto)
            => Ok(await _mediator.Send(new CreateMapEntryCommand(dto)));

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditMapEntry")]
        public async Task<ActionResult<MapEntryDto>> Update(int id, [FromBody] MapEntryRequestDto dto)
            => Ok(await _mediator.Send(new UpdateMapEntryCommand(id, dto)));

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteMapEntry")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteMapEntryCommand(id));
            return Ok(new { message = "تم حذف السجل" });
        }
    }
}
