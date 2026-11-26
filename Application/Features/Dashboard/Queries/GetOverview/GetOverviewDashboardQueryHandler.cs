using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Dashboard.Queries.GetOverview
{
    public class GetOverviewDashboardQueryHandler : IRequestHandler<GetOverviewDashboardQuery, OverviewDashboardDto>
    {
        private readonly IDashboardService _dashboardService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public GetOverviewDashboardQueryHandler(IDashboardService dashboardService, IUserService userService, IUserPermissionService permissions)
        {
            _dashboardService = dashboardService;
            _userService = userService;
            _permissions = permissions;
        }

        public async Task<OverviewDashboardDto> Handle(GetOverviewDashboardQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            if (!viewer.IsOrganizationWide) throw new UnauthorizedAccessException(DashboardSupport.Denied);
            return await _dashboardService.GetOverviewAsync();
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
