using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;
using static Application.Features.MapLayers.MapLayerRules;

namespace Application.Features.MapLayers.EntryFiles.Queries.Get
{
    public class GetMapEntryFileQueryHandler : IRequestHandler<GetMapEntryFileQuery, MapEntryFileContentDto>
    {
        private readonly IMapEntryFileService _files;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly IFileStore _store;

        public GetMapEntryFileQueryHandler(IMapEntryFileService files, IUserService users, IUserPermissionService permissions, IFileStore store)
        {
            _files = files;
            _users = users;
            _permissions = permissions;
            _store = store;
        }

        public async Task<MapEntryFileContentDto> Handle(GetMapEntryFileQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var file = await _files.GetByIdAsync(request.Id) ?? throw new KeyNotFoundException("الملف غير موجود");
            // المعلّق لرافعه وحده؛ المرتبط لمن يرى سجله (قسمي / فرعي / الكل)
            if (file.Entry == null)
            {
                if (file.UploadedByUserId != viewer.Id) throw new UnauthorizedAccessException("لا يمكنك فتح هذا الملف");
            }
            else EnsureEntry(viewer, file.Entry);

            var content = await StoredFiles.OpenAsync(_store, file.StorageKey, () => _files.GetDataAsync(file.Id));
            return new MapEntryFileContentDto(file.FileName, file.ContentType, content, MapFileRules.IsPreviewable(file.ContentType));
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
