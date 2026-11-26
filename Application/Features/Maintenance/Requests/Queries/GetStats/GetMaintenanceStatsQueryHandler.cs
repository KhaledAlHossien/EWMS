using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Maintenance.Requests.Queries.GetStats
{
    public class GetMaintenanceStatsQueryHandler : IRequestHandler<GetMaintenanceStatsQuery, MaintenanceStatsDto>
    {
        private readonly IMaintenanceRequestService _requestService;
        private readonly IMaintenanceTaskService _taskService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public GetMaintenanceStatsQueryHandler(IMaintenanceRequestService requestService, IMaintenanceTaskService taskService, IUserService userService, IUserPermissionService permissions)
        {
            _requestService = requestService;
            _taskService = taskService;
            _userService = userService;
            _permissions = permissions;
        }

        public async Task<MaintenanceStatsDto> Handle(GetMaintenanceStatsQuery request, CancellationToken ct)
        {
            var stats = await _requestService.GetStatsAsync(
                MaintenanceRules.RequestFilter(await MaintenanceRules.BoundaryAsync(_userService, _permissions, AppPermissions.ViewMaintenanceStats)));
            await _taskService.FillStatsAsync(
                MaintenanceRules.TaskFilter(await MaintenanceRules.BoundaryAsync(_userService, _permissions, AppPermissions.ViewMaintenanceStats)), stats);
            return stats;
        }
    }
}
