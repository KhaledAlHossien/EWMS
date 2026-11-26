using Application.Common;
using Application.Interfaces;
using MediatR;

namespace Application.Features.MapLayers.EntryFiles.Commands.DeletePending
{
    public class DeletePendingMapEntryFileCommandHandler : IRequestHandler<DeletePendingMapEntryFileCommand, Unit>
    {
        private readonly IMapEntryFileService _files;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;

        public DeletePendingMapEntryFileCommandHandler(IMapEntryFileService files, IUserService users, IUserPermissionService permissions)
        {
            _files = files;
            _users = users;
            _permissions = permissions;
        }

        public async Task<Unit> Handle(DeletePendingMapEntryFileCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var file = await _files.GetByIdAsync(request.Id) ?? throw new KeyNotFoundException("الملف غير موجود");
            if (file.EntryId != null) throw new InvalidOperationException("الملف مرتبط بسجل — أزله من نموذج السجل ثم احفظ");
            if (file.UploadedByUserId != viewer.Id) throw new UnauthorizedAccessException("لا يمكنك حذف هذا الملف");
            await _files.DeleteAsync(file);
            return Unit.Value;
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
