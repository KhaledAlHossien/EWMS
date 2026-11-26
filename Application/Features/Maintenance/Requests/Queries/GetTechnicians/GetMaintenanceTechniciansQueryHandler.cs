using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Maintenance.Requests.Queries.GetTechnicians
{
    public class GetMaintenanceTechniciansQueryHandler : IRequestHandler<GetMaintenanceTechniciansQuery, List<TechnicianOptionDto>>
    {
        private readonly IMaintenanceRequestService _requestService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public GetMaintenanceTechniciansQueryHandler(IMaintenanceRequestService requestService, IUserService userService, IUserPermissionService permissions)
        {
            _requestService = requestService;
            _userService = userService;
            _permissions = permissions;
        }

        public async Task<List<TechnicianOptionDto>> Handle(GetMaintenanceTechniciansQuery request, CancellationToken ct)
        {
            var view = await MaintenanceRules.BoundaryAsync(_userService, _permissions, "ViewMaintenanceRequests");
            return await _requestService.GetTechniciansAsync(MaintenanceRules.RequestFilter(view));
        }
    }
}
