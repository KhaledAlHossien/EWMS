using Application.Common;
using Application.Interfaces;
using MediatR;

namespace Application.Features.MapLayers.Entries.Commands.Delete
{
    public class DeleteMapEntryCommandHandler : IRequestHandler<DeleteMapEntryCommand, Unit>
    {
        private readonly IMapEntryService _entries;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly MapEntrySupport _support;

        public DeleteMapEntryCommandHandler(IMapEntryService entries, IUserService users, IUserPermissionService permissions, MapEntrySupport support)
        {
            _entries = entries;
            _users = users;
            _permissions = permissions;
            _support = support;
        }

        public async Task<Unit> Handle(DeleteMapEntryCommand request, CancellationToken ct)
        {
            var entry = await _support.LoadAsync(request.Id, await CurrentAsync());
            await _entries.DeleteAsync(entry);
            return Unit.Value;
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
