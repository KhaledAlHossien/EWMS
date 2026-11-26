using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Dashboard.Queries.GetDepartment
{
    public class GetDepartmentDashboardQueryHandler : IRequestHandler<GetDepartmentDashboardQuery, DepartmentDashboardDto>
    {
        private readonly IDashboardService _dashboardService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly IDepartmentService _departmentService;

        public GetDepartmentDashboardQueryHandler(IDashboardService dashboardService, IUserService userService, IUserPermissionService permissions, IDepartmentService departmentService)
        {
            _dashboardService = dashboardService;
            _userService = userService;
            _permissions = permissions;
            _departmentService = departmentService;
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

            var allowed = viewer.IsOrganizationWide
                || (viewer.Has(AppPermissions.ViewBranchDashboard) && department.BranchId == user.BranchId)
                || (viewer.Has(AppPermissions.ViewDepartmentDashboard) && department.Id == user.DepartmentId);
            if (!allowed) throw new UnauthorizedAccessException(DashboardSupport.Denied);

            return await _dashboardService.GetDepartmentAsync(departmentId)
                ?? throw new KeyNotFoundException("القسم غير موجود");
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
