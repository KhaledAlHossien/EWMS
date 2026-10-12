using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Reports;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// مركز التقارير (2026-10-12): يفتحه من يملك صلاحية سحب أي تقرير (AnyReportExport)، وصلاحية كل تقرير وحدّ سجلاته
    /// في الـ handler. المعاينة لا تُسجَّل؛ Excel والطباعة يُسجَّلان في سجل السحب (ViewReportLog).
    /// </summary>
    [ApiController]
    [Route("api/Reports")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private readonly IMediator _mediator;

        public ReportsController(IMediator mediator) => _mediator = mediator;

        [HttpGet("Catalog")]
        [Authorize(Policy = "AnyReportExport")]
        public async Task<ActionResult<List<ReportInfoDto>>> Catalog()
            => Ok(await _mediator.Send(new GetReportCatalogQuery()));

        /// <summary>تعريف التقرير وفلاتره (خياراتها ضمن حدود المستخدم)</summary>
        [HttpGet("Definition/{key}")]
        [Authorize(Policy = "AnyReportExport")]
        public async Task<ActionResult<ReportDefinitionDto>> Definition(string key)
            => Ok(await _mediator.Send(new GetReportDefinitionQuery(key)));

        [HttpPost("Preview/{key}")]
        [Authorize(Policy = "AnyReportExport")]
        public async Task<ActionResult<ReportPreviewDto>> Preview(string key, [FromBody] ReportRunRequestDto request)
            => Ok(await _mediator.Send(new PreviewReportQuery(key, request)));

        [HttpPost("Export/{key}")]
        [Authorize(Policy = "AnyReportExport")]
        public async Task<IActionResult> Export(string key, [FromBody] ReportRunRequestDto request)
        {
            var file = await _mediator.Send(new ExportReportQuery(key, request));
            return File(file.Content, Xlsx, file.FileName);
        }

        /// <summary>بيانات صفحة الطباعة A4 (الجدول كاملاً + الترويسة)</summary>
        [HttpPost("Print/{key}")]
        [Authorize(Policy = "AnyReportExport")]
        public async Task<ActionResult<ReportPrintDto>> Print(string key, [FromBody] ReportRunRequestDto request)
            => Ok(await _mediator.Send(new PrintReportQuery(key, request)));

        [HttpGet("Log")]
        [Authorize(Policy = "ViewReportLog")]
        public async Task<ActionResult<PagedResultDto<ReportLogDto>>> Log([FromQuery] ReportLogFilterDto filter)
            => Ok(await _mediator.Send(new GetReportLogQuery(filter)));

        [HttpGet("LogOptions")]
        [Authorize(Policy = "ViewReportLog")]
        public async Task<ActionResult<List<ReportOptionDto>>> LogOptions()
            => Ok(await _mediator.Send(new GetReportLogOptionsQuery()));
    }
}
