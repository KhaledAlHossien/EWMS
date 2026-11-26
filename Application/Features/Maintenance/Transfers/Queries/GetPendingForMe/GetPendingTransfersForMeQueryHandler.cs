using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Maintenance.Transfers.Queries.GetPendingForMe
{
    public class GetPendingTransfersForMeQueryHandler : IRequestHandler<GetPendingTransfersForMeQuery, List<MaintenanceTransferDto>>
    {
        private readonly IMaintenanceRequestService _requests;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;

        public GetPendingTransfersForMeQueryHandler(IMaintenanceRequestService requests, IUserService users, IUserPermissionService permissions)
        {
            _requests = requests;
            _users = users;
            _permissions = permissions;
        }

        public async Task<List<MaintenanceTransferDto>> Handle(GetPendingTransfersForMeQuery request, CancellationToken ct)
        {
            var assign = await MaintenanceRules.BoundaryAsync(_users, _permissions, "AssignMaintenanceRequest");
            if (assign == null) return [];
            var list = await _requests.GetPendingTransfersAsync(assign.All ? null : assign.DepartmentId);
            return list.Select(MaintenanceTransferSupport.ToDto).ToList();
        }
    }
}
