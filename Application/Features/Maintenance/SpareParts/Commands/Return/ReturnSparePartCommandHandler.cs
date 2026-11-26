using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Commands.Return
{
    public class ReturnSparePartCommandHandler : IRequestHandler<ReturnSparePartCommand, RequestPartsDto>
    {
        private readonly ISparePartService _service;
        private readonly IMaintenanceRequestService _requestService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly SparePartSupport _support;

        public ReturnSparePartCommandHandler(ISparePartService service, IMaintenanceRequestService requestService, IUserService userService, IUserPermissionService permissions, SparePartSupport support)
        {
            _service = service;
            _requestService = requestService;
            _userService = userService;
            _permissions = permissions;
            _support = support;
        }

        public async Task<RequestPartsDto> Handle(ReturnSparePartCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var requestPart = await _service.GetRequestPartAsync(request.RequestPartId)
                ?? throw new KeyNotFoundException("القطعة غير موجودة على الطلب");
            var r = await LoadRequestAsync(requestPart.MaintenanceRequestId);
            await _support.EnsureCanIssueAsync(r);

            var (quantity, unit, name) = (requestPart.Quantity, requestPart.SparePart.Unit, requestPart.SparePart.Name);
            await _service.ReturnAsync(requestPart, viewer.Id);

            await _support.LogAsync(r, viewer.User, MaintenanceActivityType.PartReturned,
                $"أعاد {SparePartRules.Amount(quantity)} {unit} من «{name}» إلى المخزون");

            return await _support.RequestPartsAsync(r);
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_userService, _permissions);

        private async Task<MaintenanceRequest> LoadRequestAsync(int id) =>
            await _requestService.GetByIdAsync(id) ?? throw new KeyNotFoundException("طلب الصيانة غير موجود");
    }
}
