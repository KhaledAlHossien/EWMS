using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Dashboard.Queries.GetMy
{
    public class GetMyDashboardQueryHandler : IRequestHandler<GetMyDashboardQuery, EmployeeDashboardDto>
    {
        private readonly IDashboardService _dashboardService;
        private readonly IUserService _userService;

        public GetMyDashboardQueryHandler(IDashboardService dashboardService, IUserService userService)
        {
            _dashboardService = dashboardService;
            _userService = userService;
        }

        public async Task<EmployeeDashboardDto> Handle(GetMyDashboardQuery request, CancellationToken ct)
        {
            return await _dashboardService.GetEmployeeAsync(_userService.UserId)
                ?? throw new UnauthorizedAccessException("المستخدم غير مصادق");
        }
    }
}
