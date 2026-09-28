using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.Features.Dashboard
{
    /// <summary>
    /// نطاق الداشبوردات (قرار المستخدم 2026-09-28):
    /// - SuperAdmin يطّلع على كل شيء (نظرة عامة + أي فرع/قسم/مكتب).
    /// - رئيس الفرع: فرعه وأقسامه ومكاتبه. رئيس القسم: قسمه ومكاتبه. رئيس المكتب: مكتبه.
    /// - كل مستخدم: داشبورده الشخصي (Me).
    /// عند عدم تمرير id يُستخدم نطاق المستخدم نفسه.
    /// </summary>
    public record GetOverviewDashboardQuery : IRequest<OverviewDashboardDto>;
    public record GetBranchDashboardQuery(int? BranchId) : IRequest<BranchDashboardDto>;
    public record GetDepartmentDashboardQuery(int? DepartmentId) : IRequest<DepartmentDashboardDto>;
    public record GetOfficeDashboardQuery(int? OfficeId) : IRequest<OfficeDashboardDto>;
    public record GetMyDashboardQuery : IRequest<EmployeeDashboardDto>;

    /// <summary>
    /// صفحة إحصائيات الإجازات المستقلة للرؤساء: نطاق كل رئيس هو فرعه/قسمه/مكتبه،
    /// و SuperAdmin يرى المؤسسة كلها أو يختار فرعاً.
    /// </summary>
    public record GetVacationStatsQuery(int? BranchId) : IRequest<VacationStatsDto>;

    public class DashboardQueriesHandler :
        IRequestHandler<GetOverviewDashboardQuery, OverviewDashboardDto>,
        IRequestHandler<GetBranchDashboardQuery, BranchDashboardDto>,
        IRequestHandler<GetDepartmentDashboardQuery, DepartmentDashboardDto>,
        IRequestHandler<GetOfficeDashboardQuery, OfficeDashboardDto>,
        IRequestHandler<GetMyDashboardQuery, EmployeeDashboardDto>,
        IRequestHandler<GetVacationStatsQuery, VacationStatsDto>
    {
        private const string Denied = "لا تملك صلاحية الاطلاع على هذه اللوحة";

        private readonly IDashboardService _dashboardService;
        private readonly IUserService _userService;
        private readonly IDepartmentService _departmentService;
        private readonly IOfficeService _officeService;

        public DashboardQueriesHandler(
            IDashboardService dashboardService,
            IUserService userService,
            IDepartmentService departmentService,
            IOfficeService officeService)
        {
            _dashboardService = dashboardService;
            _userService = userService;
            _departmentService = departmentService;
            _officeService = officeService;
        }

        private async Task<(User User, string Role)> CurrentAsync()
        {
            var user = await _userService.GetByIdAsync(_userService.UserId)
                ?? throw new UnauthorizedAccessException("المستخدم غير مصادق");
            return (user, user.Role?.Name ?? "");
        }

        public async Task<OverviewDashboardDto> Handle(GetOverviewDashboardQuery request, CancellationToken ct)
        {
            var (_, role) = await CurrentAsync();
            if (role != "SuperAdmin") throw new UnauthorizedAccessException(Denied);
            return await _dashboardService.GetOverviewAsync();
        }

        public async Task<BranchDashboardDto> Handle(GetBranchDashboardQuery request, CancellationToken ct)
        {
            var (user, role) = await CurrentAsync();

            var branchId = role switch
            {
                "SuperAdmin" => request.BranchId ?? throw new ArgumentException("حدد الفرع"),
                "BranchManager" when request.BranchId == null || request.BranchId == user.BranchId
                    => user.BranchId ?? throw new InvalidOperationException("حسابك غير مرتبط بفرع"),
                _ => throw new UnauthorizedAccessException(Denied)
            };

            return await _dashboardService.GetBranchAsync(branchId)
                ?? throw new KeyNotFoundException("الفرع غير موجود");
        }

        public async Task<DepartmentDashboardDto> Handle(GetDepartmentDashboardQuery request, CancellationToken ct)
        {
            var (user, role) = await CurrentAsync();

            var departmentId = request.DepartmentId
                ?? (role == "Manager" ? user.DepartmentId : null)
                ?? throw new ArgumentException("حدد القسم");

            var department = await _departmentService.GetByIdAsync(departmentId)
                ?? throw new KeyNotFoundException("القسم غير موجود");

            var allowed = role switch
            {
                "SuperAdmin" => true,
                "BranchManager" => department.BranchId == user.BranchId,
                "Manager" => department.Id == user.DepartmentId,
                _ => false
            };
            if (!allowed) throw new UnauthorizedAccessException(Denied);

            return await _dashboardService.GetDepartmentAsync(departmentId)
                ?? throw new KeyNotFoundException("القسم غير موجود");
        }

        public async Task<OfficeDashboardDto> Handle(GetOfficeDashboardQuery request, CancellationToken ct)
        {
            var (user, role) = await CurrentAsync();

            var officeId = request.OfficeId
                ?? (role == "OfficeManager" ? user.OfficeId : null)
                ?? throw new ArgumentException("حدد المكتب");

            var office = await _officeService.GetByIdAsync(officeId)
                ?? throw new KeyNotFoundException("المكتب غير موجود");

            var allowed = role switch
            {
                "SuperAdmin" => true,
                "BranchManager" => office.Department?.BranchId == user.BranchId,
                "Manager" => office.DepartmentId == user.DepartmentId,
                "OfficeManager" => office.Id == user.OfficeId,
                _ => false
            };
            if (!allowed) throw new UnauthorizedAccessException(Denied);

            return await _dashboardService.GetOfficeAsync(officeId)
                ?? throw new KeyNotFoundException("المكتب غير موجود");
        }

        public async Task<EmployeeDashboardDto> Handle(GetMyDashboardQuery request, CancellationToken ct)
        {
            return await _dashboardService.GetEmployeeAsync(_userService.UserId)
                ?? throw new UnauthorizedAccessException("المستخدم غير مصادق");
        }

        public async Task<VacationStatsDto> Handle(GetVacationStatsQuery request, CancellationToken ct)
        {
            var (user, role) = await CurrentAsync();
            const string noPlacement = "حسابك غير مرتبط بالنطاق المطلوب";

            var (scope, id) = role switch
            {
                "SuperAdmin" => request.BranchId is int b ? (DashboardScope.Branch, (int?)b) : (DashboardScope.All, null),
                "BranchManager" => (DashboardScope.Branch, user.BranchId ?? throw new InvalidOperationException(noPlacement)),
                "Manager" => (DashboardScope.Department, user.DepartmentId ?? throw new InvalidOperationException(noPlacement)),
                "OfficeManager" => (DashboardScope.Office, user.OfficeId ?? throw new InvalidOperationException(noPlacement)),
                _ => throw new UnauthorizedAccessException(Denied)
            };

            return await _dashboardService.GetVacationStatsAsync(scope, id)
                ?? throw new KeyNotFoundException("النطاق غير موجود");
        }
    }
}
