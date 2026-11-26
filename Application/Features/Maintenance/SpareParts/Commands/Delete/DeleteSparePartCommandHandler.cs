using Application.Common;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Commands.Delete
{
    public class DeleteSparePartCommandHandler : IRequestHandler<DeleteSparePartCommand, Unit>
    {
        private readonly ISparePartService _service;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly SparePartSupport _support;

        public DeleteSparePartCommandHandler(ISparePartService service, IUserService userService, IUserPermissionService permissions, SparePartSupport support)
        {
            _service = service;
            _userService = userService;
            _permissions = permissions;
            _support = support;
        }

        public async Task<Unit> Handle(DeleteSparePartCommand request, CancellationToken ct)
        {
            var part = await _support.LoadInScopeAsync(request.Id, await ViewerAsync());

            if (await _service.HasMovementsAsync(part.Id))
                throw new InvalidOperationException("لا يمكن حذف قطعة لها حركات مخزون — سجلها محفوظ");

            await _service.DeleteAsync(part);
            return Unit.Value;
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
