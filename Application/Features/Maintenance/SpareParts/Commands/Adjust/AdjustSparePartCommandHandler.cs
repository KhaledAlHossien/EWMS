using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Commands.Adjust
{
    public class AdjustSparePartCommandHandler : IRequestHandler<AdjustSparePartCommand, SparePartResponseDto>
    {
        private readonly ISparePartService _service;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly INotificationService _notifications;
        private readonly SparePartSupport _support;

        public AdjustSparePartCommandHandler(ISparePartService service, IUserService userService, IUserPermissionService permissions, INotificationService notifications, SparePartSupport support)
        {
            _service = service;
            _userService = userService;
            _permissions = permissions;
            _notifications = notifications;
            _support = support;
        }

        public async Task<SparePartResponseDto> Handle(AdjustSparePartCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var part = await _support.LoadInScopeAsync(request.Id, viewer);

            var change = await _service.AdjustAsync(part.Id, request.Dto.Delta, request.Dto.Reason.Trim(), viewer.Id);
            if (SparePartRules.CrossedBelowMinimum(part, change))
                await MaintenanceNotifier.LowStockAsync(_notifications, _permissions, part, change.After, viewer.User);

            return await _support.ReloadDtoAsync(part.Id);
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
