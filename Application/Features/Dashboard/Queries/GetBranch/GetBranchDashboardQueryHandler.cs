using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Dashboard.Queries.GetBranch
{
    public class GetBranchDashboardQueryHandler : IRequestHandler<GetBranchDashboardQuery, BranchDashboardDto>
    {
        private readonly IDashboardService _dashboardService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public GetBranchDashboardQueryHandler(IDashboardService dashboardService, IUserService userService, IUserPermissionService permissions)
        {
            _dashboardService = dashboardService;
            _userService = userService;
            _permissions = permissions;
        }

        public async Task<BranchDashboardDto> Handle(GetBranchDashboardQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var user = viewer.User;
            var branchId = viewer.IsOrganizationWide ? request.BranchId ?? throw new ArgumentException("حدد الفرع")
                : viewer.Has(AppPermissions.ViewBranchDashboard) && (request.BranchId == null || request.BranchId == user.BranchId)
                    ? user.BranchId ?? throw new InvalidOperationException("حسابك غير مرتبط بفرع")
                    : throw new UnauthorizedAccessException(DashboardSupport.Denied);

            return await _dashboardService.GetBranchAsync(branchId)
                ?? throw new KeyNotFoundException("الفرع غير موجود");
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
