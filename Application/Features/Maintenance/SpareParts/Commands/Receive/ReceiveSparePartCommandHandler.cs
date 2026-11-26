using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Commands.Receive
{
    public class ReceiveSparePartCommandHandler : IRequestHandler<ReceiveSparePartCommand, SparePartResponseDto>
    {
        private readonly ISparePartService _service;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly SparePartSupport _support;

        public ReceiveSparePartCommandHandler(ISparePartService service, IUserService userService, IUserPermissionService permissions, SparePartSupport support)
        {
            _service = service;
            _userService = userService;
            _permissions = permissions;
            _support = support;
        }

        public async Task<SparePartResponseDto> Handle(ReceiveSparePartCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var part = await _support.LoadInScopeAsync(request.Id, viewer);
            var dto = request.Dto;

            await _service.ReceiveAsync(part.Id, dto.Quantity, dto.UnitCost, dto.Date ?? DateTime.Today, dto.Source.Trim(), viewer.Id);
            return await _support.ReloadDtoAsync(part.Id);
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
