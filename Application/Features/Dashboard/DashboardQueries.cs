using Application.DTOs.Response;
using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.Features.Dashboard
{
    /// <summary>
    /// نطاق الداشبوردات (قرار المستخدم 2026-09-28):
    /// - SuperAdmin يطّلع على كل شيء (نظرة عامة + أي فرع/قسم/مكتب).
    /// - ViewBranchDashboard: فرعه وأقسامه ومكاتبه. ViewDepartmentDashboard: قسمه ومكاتبه. ViewOfficeDashboard: مكتبه (Role-Permission، 2026-10-03).
    /// - كل مستخدم: داشبورده الشخصي (Me).
    /// عند عدم تمرير id يُستخدم نطاق المستخدم نفسه.
    /// </summary>
    public record GetOverviewDashboardQuery : IRequest<OverviewDashboardDto>;
    public record GetBranchDashboardQuery(int? BranchId) : IRequest<BranchDashboardDto>;
    public record GetDepartmentDashboardQuery(int? DepartmentId) : IRequest<DepartmentDashboardDto>;
    public record GetOfficeDashboardQuery(int? OfficeId) : IRequest<OfficeDashboardDto>;
    public record GetMyDashboardQuery : IRequest<EmployeeDashboardDto>;

    /// <summary>
    /// صفحة إحصائيات الإجازات المستقلة: ViewBranchVacations → فرعه، ViewDepartmentVacations → قسمه،
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
        private readonly IUserPermissionService _permissions;
        private readonly IDepartmentService _departmentService;
        private readonly IOfficeService _officeService;

        public DashboardQueriesHandler(
            IDashboardService dashboardService,
            IUserService userService,
            IUserPermissionService permissions,
            IDepartmentService departmentService,
            IOfficeService officeService)
        {
            _dashboardService = dashboardService;
            _userService = userService;
            _permissions = permissions;
            _departmentService = departmentService;
            _officeService = officeService;
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_userService, _permissions);

        public async Task<OverviewDashboardDto> Handle(GetOverviewDashboardQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            if (!viewer.IsSuperAdmin && !viewer.Has(AppPermissions.ViewOrganizationDashboard)) throw new UnauthorizedAccessException(Denied);
            return await _dashboardService.GetOverviewAsync();
        }

        public async Task<BranchDashboardDto> Handle(GetBranchDashboardQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var user = viewer.User;
            var branchId = viewer.IsSuperAdmin ? request.BranchId ?? throw new ArgumentException("حدد الفرع")
                : viewer.Has(AppPermissions.ViewBranchDashboard) && (request.BranchId == null || request.BranchId == user.BranchId)
                    ? user.BranchId ?? throw new InvalidOperationException("حسابك غير مرتبط بفرع")
                    : throw new UnauthorizedAccessException(Denied);

            return await _dashboardService.GetBranchAsync(branchId)
                ?? throw new KeyNotFoundException("الفرع غير موجود");
        }

        public async Task<DepartmentDashboardDto> Handle(GetDepartmentDashboardQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var user = viewer.User;

            var departmentId = request.DepartmentId
                ?? (viewer.Has(AppPermissions.ViewDepartmentDashboard) ? user.DepartmentId : null)
                ?? throw new ArgumentException("حدد القسم");

            var department = await _departmentService.GetByIdAsync(departmentId)
                ?? throw new KeyNotFoundException("القسم غير موجود");

            var allowed = viewer.IsSuperAdmin
                || (viewer.Has(AppPermissions.ViewBranchDashboard) && department.BranchId == user.BranchId)
                || (viewer.Has(AppPermissions.ViewDepartmentDashboard) && department.Id == user.DepartmentId);
            if (!allowed) throw new UnauthorizedAccessException(Denied);

            return await _dashboardService.GetDepartmentAsync(departmentId)
                ?? throw new KeyNotFoundException("القسم غير موجود");
        }

        public async Task<OfficeDashboardDto> Handle(GetOfficeDashboardQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var user = viewer.User;

            var officeId = request.OfficeId
                ?? (viewer.Has(AppPermissions.ViewOfficeDashboard) ? user.OfficeId : null)
                ?? throw new ArgumentException("حدد المكتب");

            var office = await _officeService.GetByIdAsync(officeId)
                ?? throw new KeyNotFoundException("المكتب غير موجود");

            var allowed = viewer.IsSuperAdmin
                || (viewer.Has(AppPermissions.ViewBranchDashboard) && office.Department?.BranchId == user.BranchId)
                || (viewer.Has(AppPermissions.ViewDepartmentDashboard) && office.DepartmentId == user.DepartmentId)
                || (viewer.Has(AppPermissions.ViewOfficeDashboard) && office.Id == user.OfficeId);
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
            var viewer = await CurrentAsync();
            var user = viewer.User;
            const string noPlacement = "حسابك غير مرتبط بالنطاق المطلوب";

            // ViewBranchVacations → إحصائيات فرعي، ViewDepartmentVacations → قسمي (الأوسع إن اجتمعتا)
            var (scope, id) = viewer.IsSuperAdmin ? (request.BranchId is int b ? DashboardScope.Branch : DashboardScope.All, request.BranchId)
                : viewer.Has(AppPermissions.ViewBranchVacations) ? (DashboardScope.Branch, user.BranchId ?? throw new InvalidOperationException(noPlacement))
                : viewer.Has(AppPermissions.ViewDepartmentVacations) ? (DashboardScope.Department, user.DepartmentId ?? throw new InvalidOperationException(noPlacement))
                : throw new UnauthorizedAccessException("لا تملك صلاحية الاطلاع على إحصائيات الإجازات");

            return await _dashboardService.GetVacationStatsAsync(scope, id)
                ?? throw new KeyNotFoundException("النطاق غير موجود");
        }
    }
}
