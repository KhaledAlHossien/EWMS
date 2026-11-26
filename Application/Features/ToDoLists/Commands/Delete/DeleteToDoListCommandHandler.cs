using Application.Common;
using Application.Interfaces;
using MediatR;

namespace Application.Features.ToDoLists.Commands.Delete
{
    public class DeleteToDoListCommandHandler : IRequestHandler<DeleteToDoListCommand, Unit>
    {
        private readonly IToDoListService _service;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly ToDoListSupport _support;

        public DeleteToDoListCommandHandler(IToDoListService service, IUserService userService, IUserPermissionService permissions, ToDoListSupport support)
        {
            _service = service;
            _userService = userService;
            _permissions = permissions;
            _support = support;
        }

        public async Task<Unit> Handle(DeleteToDoListCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var entity = await _support.LoadOwnedAsync(viewer, request.Id, "لا يمكنك حذف قائمة غيرك");

            await _service.DeleteAsync(entity);
            return Unit.Value;
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
