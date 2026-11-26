using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Dashboard.Queries.GetOffice
{
    public class GetOfficeDashboardQueryHandler : IRequestHandler<GetOfficeDashboardQuery, OfficeDashboardDto>
    {
        private readonly IDashboardService _dashboardService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly IOfficeService _officeService;

        public GetOfficeDashboardQueryHandler(IDashboardService dashboardService, IUserService userService, IUserPermissionService permissions, IOfficeService officeService)
        {
            _dashboardService = dashboardService;
            _userService = userService;
            _permissions = permissions;
            _officeService = officeService;
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

            var allowed = viewer.IsOrganizationWide
                || (viewer.Has(AppPermissions.ViewBranchDashboard) && office.Department?.BranchId == user.BranchId)
                || (viewer.Has(AppPermissions.ViewDepartmentDashboard) && office.DepartmentId == user.DepartmentId)
                || (viewer.Has(AppPermissions.ViewOfficeDashboard) && office.Id == user.OfficeId);
            if (!allowed) throw new UnauthorizedAccessException(DashboardSupport.Denied);

            return await _dashboardService.GetOfficeAsync(officeId)
                ?? throw new KeyNotFoundException("المكتب غير موجود");
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
