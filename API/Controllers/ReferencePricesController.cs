using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.ReferencePrices;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// الأسعار المرجعية (مكتب التسعير): سجل تاريخي لأسعار المواد عند الموردين بمصدرها وتاريخها. لا تعديل ولا حذف: التصحيح بسجل بديل.
    /// الإضافة غير الاعتماد (من أضاف لا يعتمد). «آخر سعر» هو الأحدث تاريخاً لمادة بوحدة وعملة واحدة — لا الأعلى ولا الأقل، ولا مقارنة بين وحدات أو عملات.
    /// الحدّ فرع السجل (ونظرة المؤسسة كل الفروع) ويُفحص داخل المعالج.
    /// </summary>
    [ApiController]
    [Route("api/ReferencePrices")]
    [Authorize]
    public class ReferencePricesController : ControllerBase
    {
        private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private readonly IMediator _mediator;

        public ReferencePricesController(IMediator mediator) => _mediator = mediator;

        /// <summary>بحث مرقّم (الأحدث تاريخاً أولاً). status: 1 تاريخي، 2 معتمد للاستخدام الحالي، 3 مُستبدَل. المُستبدَل مخفي ما لم يُطلب</summary>
        [HttpGet("Search")]
        [Authorize(Policy = "AnyReferencePriceView")]
        public async Task<ActionResult<PagedResultDto<ReferencePriceListItemDto>>> Search(
            [FromQuery] int? branchId, [FromQuery] string? search, [FromQuery] int? supplierId, [FromQuery] int? projectId, [FromQuery] int? status,
            [FromQuery] bool includeSuperseded = false, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(await _mediator.Send(new SearchReferencePricesQuery(branchId, search, supplierId, projectId, status, includeSuperseded, from, to, page, pageSize)));

        /// <summary>التفصيل بسلسلة التصحيح وتاريخ المادة (بالوحدة والعملة نفسهما)</summary>
        [HttpGet("Get/{id}")]
        [Authorize(Policy = "AnyReferencePriceView")]
        public async Task<ActionResult<ReferencePriceDetailDto>> Get(int id) => Ok(await _mediator.Send(new GetReferencePriceQuery(id)));

        [HttpPost("Create")]
        [Authorize(Policy = "AddReferencePrice")]
        public async Task<ActionResult<ReferencePriceDetailDto>> Create([FromBody] ReferencePriceRequestDto dto) => Ok(await _mediator.Send(new CreateReferencePriceCommand(dto)));

        /// <summary>تصحيح السعر بسجل بديل بسبب مكتوب؛ المُصحَّح يبقى في التاريخ</summary>
        [HttpPost("Correct/{id}")]
        [Authorize(Policy = "AddReferencePrice")]
        public async Task<ActionResult<ReferencePriceDetailDto>> Correct(int id, [FromBody] CorrectReferencePriceRequestDto dto) => Ok(await _mediator.Send(new CorrectReferencePriceCommand(id, dto)));

        /// <summary>قالب استيراد الأسعار المرجعية (Excel): الأعمدة بالترتيب مع سطر مثال وورقة تعليمات</summary>
        [HttpGet("ImportTemplate")]
        [Authorize(Policy = "ImportReferencePrices")]
        public async Task<IActionResult> ImportTemplate()
            => File(await _mediator.Send(new GetReferencePriceImportTemplateQuery()), Xlsx, "reference-prices-template.xlsx");

        /// <summary>
        /// استيراد ملف Excel: dryRun=true معاينة بلا حفظ (الافتراضي)، false يحفظ كل الأسعار بعد فحص كامل — الكل أو لا شيء: سطر خاطئ واحد يمنع الحفظ كله.
        /// branchId اختياري لنظرة المؤسسة (فرع الأسعار)، وإلا فرع المستورِد. الأسعار المستوردة تاريخية بانتظار اعتماد غير المستورِد.
        /// </summary>
        [HttpPost("Import")]
        [Authorize(Policy = "ImportReferencePrices")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<ActionResult<ReferencePriceImportReportDto>> Import(IFormFile? file, [FromQuery] bool dryRun = true, [FromQuery] int? branchId = null)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "اختر ملف Excel" });
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { message = "حجم الملف يتجاوز 5MB" });

            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;
            return Ok(await _mediator.Send(new ImportReferencePricesCommand(stream, file.FileName, dryRun, branchId)));
        }

        /// <summary>اعتماد السعر للاستخدام الحالي (غير من أضافه)</summary>
        [HttpPost("Approve/{id}")]
        [Authorize(Policy = "ApproveReferencePrice")]
        public async Task<ActionResult<ReferencePriceDetailDto>> Approve(int id, [FromBody] PricingStepRequestDto dto) => Ok(await _mediator.Send(new ApproveReferencePriceCommand(id, dto)));
    }
}
