using Application.Interfaces;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.Requests.Commands.Delete
{
    public class DeleteMaintenanceRequestCommandHandler : IRequestHandler<DeleteMaintenanceRequestCommand, Unit>
    {
        private readonly IMaintenanceRequestService _requestService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly ISparePartService _spareParts;

        public DeleteMaintenanceRequestCommandHandler(IMaintenanceRequestService requestService, IUserService userService, IUserPermissionService permissions, ISparePartService spareParts)
        {
            _requestService = requestService;
            _userService = userService;
            _permissions = permissions;
            _spareParts = spareParts;
        }

        public async Task<Unit> Handle(DeleteMaintenanceRequestCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await LoadAsync(request.Id);

            MaintenanceRules.Ensure(MaintenanceRules.In(await MaintenanceRules.BoundaryAsync(_userService, _permissions, "DeleteMaintenanceRequest"), entity),
                "لا يمكنك حذف طلب صيانة خارج نطاقك");

            // حركات المخزون (صرف وإرجاع) سجل لا يُحذف ويشير إلى الطلب — فلا يُحذف طلب صُرفت عليه قطع
            if (await _spareParts.HasRequestPartsAsync(entity.Id))
                throw new InvalidOperationException("لا يمكن حذف طلب صُرفت عليه قطع غيار — سجل المخزون يشير إليه");

            await _requestService.DeleteAsync(entity);
            return Unit.Value;
        }

        private async Task<MaintenanceRequest> LoadAsync(int id) =>
            await _requestService.GetByIdAsync(id) ?? throw new KeyNotFoundException("طلب الصيانة غير موجود");
    }
}
