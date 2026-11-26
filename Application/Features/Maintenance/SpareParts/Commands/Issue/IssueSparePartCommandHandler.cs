using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Commands.Issue
{
    public class IssueSparePartCommandHandler : IRequestHandler<IssueSparePartCommand, RequestPartsDto>
    {
        private readonly ISparePartService _service;
        private readonly IMaintenanceRequestService _requestService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly INotificationService _notifications;
        private readonly SparePartSupport _support;

        public IssueSparePartCommandHandler(ISparePartService service, IMaintenanceRequestService requestService, IUserService userService, IUserPermissionService permissions, INotificationService notifications, SparePartSupport support)
        {
            _service = service;
            _requestService = requestService;
            _userService = userService;
            _permissions = permissions;
            _notifications = notifications;
            _support = support;
        }

        public async Task<RequestPartsDto> Handle(IssueSparePartCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var r = await LoadRequestAsync(request.RequestId);
            await _support.EnsureCanIssueAsync(r);

            var part = await _support.LoadAsync(request.Dto.SparePartId);
            if (part.DepartmentId != r.DepartmentId)
                throw new InvalidOperationException("القطعة ليست من مخزون قسم الطلب");

            var (issued, change) = await _service.IssueAsync(r.Id, part.Id, request.Dto.Quantity, viewer.Id);

            await _support.LogAsync(r, viewer.User, MaintenanceActivityType.PartIssued,
                $"صرف {SparePartRules.Amount(issued.Quantity)} {part.Unit} من «{part.Name}» ({SparePartRules.Amount(issued.Quantity * issued.UnitCost)} ل.س)");

            if (SparePartRules.CrossedBelowMinimum(part, change))
                await MaintenanceNotifier.LowStockAsync(_notifications, _permissions, part, change.After, viewer.User);

            return await _support.RequestPartsAsync(r);
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_userService, _permissions);

        private async Task<MaintenanceRequest> LoadRequestAsync(int id) =>
            await _requestService.GetByIdAsync(id) ?? throw new KeyNotFoundException("طلب الصيانة غير موجود");
    }
}
