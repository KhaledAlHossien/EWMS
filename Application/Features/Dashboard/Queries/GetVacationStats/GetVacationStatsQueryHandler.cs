using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Dashboard.Queries.GetVacationStats
{
    public class GetVacationStatsQueryHandler : IRequestHandler<GetVacationStatsQuery, VacationStatsDto>
    {
        private readonly IDashboardService _dashboardService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public GetVacationStatsQueryHandler(IDashboardService dashboardService, IUserService userService, IUserPermissionService permissions)
        {
            _dashboardService = dashboardService;
            _userService = userService;
            _permissions = permissions;
        }

        public async Task<VacationStatsDto> Handle(GetVacationStatsQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var user = viewer.User;
            const string noPlacement = "حسابك غير مرتبط بالنطاق المطلوب";

            // ViewBranchVacations → إحصائيات فرعي، ViewDepartmentVacations → قسمي (الأوسع إن اجتمعتا)
            var (scope, id) = viewer.IsOrganizationWide ? (request.BranchId is int b ? DashboardScope.Branch : DashboardScope.All, request.BranchId)
                : viewer.Has(AppPermissions.ViewBranchVacations) ? (DashboardScope.Branch, user.BranchId ?? throw new InvalidOperationException(noPlacement))
                : viewer.Has(AppPermissions.ViewDepartmentVacations) ? (DashboardScope.Department, user.DepartmentId ?? throw new InvalidOperationException(noPlacement))
                : throw new UnauthorizedAccessException("لا تملك صلاحية الاطلاع على إحصائيات الإجازات");

            return await _dashboardService.GetVacationStatsAsync(scope, id)
                ?? throw new KeyNotFoundException("النطاق غير موجود");
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
