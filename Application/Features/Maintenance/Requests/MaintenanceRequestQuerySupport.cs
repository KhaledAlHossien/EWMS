using Application.Interfaces;
using Domain.Entities.Maintenance;

namespace Application.Features.Maintenance.Requests
{
    /// <summary>الدوال المشتركة بين عمليات هذه الميزة</summary>
    public class MaintenanceRequestQuerySupport
    {
        private readonly IMaintenanceRequestService _requestService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public MaintenanceRequestQuerySupport(IMaintenanceRequestService requestService, IUserService userService, IUserPermissionService permissions)
        {
            _requestService = requestService;
            _userService = userService;
            _permissions = permissions;
        }

        public async Task<(MaintenanceRequest Entity, MaintenanceRules.Scopes Scopes)> LoadForViewAsync(int id)
        {
            var scopes = await MaintenanceRules.RequestScopesAsync(_userService, _permissions);
            var entity = await _requestService.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("طلب الصيانة غير موجود");

            MaintenanceRules.Ensure(MaintenanceRules.In(scopes.View, entity), "لا يمكنك عرض طلب صيانة خارج نطاقك");

            return (entity, scopes);
        }
    }
}
