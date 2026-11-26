using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Maintenance.Transfers.Queries.GetPending
{
    public class GetPendingTransferQueryHandler : IRequestHandler<GetPendingTransferQuery, MaintenanceTransferDto?>
    {
        private readonly IMaintenanceRequestService _requests;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;

        public GetPendingTransferQueryHandler(IMaintenanceRequestService requests, IUserService users, IUserPermissionService permissions)
        {
            _requests = requests;
            _users = users;
            _permissions = permissions;
        }

        public async Task<MaintenanceTransferDto?> Handle(GetPendingTransferQuery request, CancellationToken ct)
        {
            var entity = await _requests.GetByIdAsync(request.RequestId)
                ?? throw new KeyNotFoundException("طلب الصيانة غير موجود");
            var scopes = await MaintenanceRules.RequestScopesAsync(_users, _permissions);
            MaintenanceRules.Ensure(MaintenanceRules.In(scopes.View, entity), "لا يمكنك عرض طلب صيانة خارج نطاقك");

            var pending = await _requests.GetPendingTransferAsync(entity.Id);
            return pending == null ? null : MaintenanceTransferSupport.ToDto(pending);
        }
    }
}
