using Application.DTOs.Response;
using Application.Features.Dashboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// لوحات المتابعة حسب الدور — النطاق يُتحقق منه داخل DashboardQueriesHandler
    /// (بدون id = نطاق المستخدم نفسه؛ SuperAdmin يمرر أي id).
    /// </summary>
    [ApiController]
    [Route("api/Dashboard")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IMediator _mediator;

        public DashboardController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("Overview")]
        [Authorize(Policy = "ViewOrganizationDashboard")]
        public async Task<ActionResult<OverviewDashboardDto>> Overview()
            => Ok(await _mediator.Send(new GetOverviewDashboardQuery()));

        [HttpGet("Branch/{id:int?}")]
        [Authorize(Policy = "ViewBranchDashboard")]
        public async Task<ActionResult<BranchDashboardDto>> Branch(int? id)
            => Ok(await _mediator.Send(new GetBranchDashboardQuery(id)));

        [HttpGet("Department/{id:int?}")]
        [Authorize(Policy = "AnyDepartmentDashboard")]
        public async Task<ActionResult<DepartmentDashboardDto>> Department(int? id)
            => Ok(await _mediator.Send(new GetDepartmentDashboardQuery(id)));

        [HttpGet("Office/{id:int?}")]
        [Authorize(Policy = "AnyOfficeDashboard")]
        public async Task<ActionResult<OfficeDashboardDto>> Office(int? id)
            => Ok(await _mediator.Send(new GetOfficeDashboardQuery(id)));

        [HttpGet("Me")]
        [Authorize(Policy = "ViewMyDashboard")]
        public async Task<ActionResult<EmployeeDashboardDto>> Me()
            => Ok(await _mediator.Send(new GetMyDashboardQuery()));

        // إحصائيات الإجازات (صفحة مستقلة للرؤساء) — SuperAdmin يمرر branchId اختيارياً
        [HttpGet("Vacations")]
        [Authorize(Policy = "AnyVacationStats")]
        public async Task<ActionResult<VacationStatsDto>> Vacations([FromQuery] int? branchId)
            => Ok(await _mediator.Send(new GetVacationStatsQuery(branchId)));
    }
}
