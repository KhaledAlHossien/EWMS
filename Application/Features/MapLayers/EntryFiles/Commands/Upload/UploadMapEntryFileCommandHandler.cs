using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.MapLayers.MapLayerRules;

namespace Application.Features.MapLayers.EntryFiles.Commands.Upload
{
    public class UploadMapEntryFileCommandHandler : IRequestHandler<UploadMapEntryFileCommand, MapEntryFileDto>
    {
        private readonly IMapEntryFileService _files;
        private readonly IMapLayerService _layers;
        private readonly IMapLayerSettings _settings;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly IFileStore _store;

        public UploadMapEntryFileCommandHandler(IMapEntryFileService files, IMapLayerService layers, IMapLayerSettings settings, IUserService users, IUserPermissionService permissions, IFileStore store)
        {
            _files = files;
            _layers = layers;
            _settings = settings;
            _users = users;
            _permissions = permissions;
            _store = store;
        }

        public async Task<MapEntryFileDto> Handle(UploadMapEntryFileCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var field = await _layers.GetFieldAsync(request.FieldId) ?? throw new KeyNotFoundException("الحقل غير موجود");
            EnsureLayer(viewer, field.Layer);
            if (!field.Layer.IsActive) throw new InvalidOperationException("الطبقة معطّلة — لا تُرفع إليها ملفات");
            if (field.Type != MapFieldType.Files || field.IsHidden) throw new InvalidOperationException("هذا الحقل لا يقبل ملفات");

            var (problem, detected, kind) = MapFileRules.Check(request.File, field, _settings.MaxFileMegabytes);
            if (problem != null) throw new InvalidOperationException(problem);

            var file = new MapEntryFile
            {
                FieldId = field.Id,
                FileName = MapFileRules.SafeFileName(request.File.FileName),
                ContentType = detected!.ContentType,
                Kind = kind,
                Size = request.File.Data.LongLength,
                UploadedByUserId = viewer.Id,
                UploadedAt = DateTime.UtcNow,
                // في مجلد المرفقات أولاً إن كان مُفعَّلاً، وإلا في القاعدة
                StorageKey = await _store.SaveAsync(FileAreas.MapFiles, request.File.Data, ct)
            };
            await _files.AddPendingAsync(file, file.StorageKey == null ? request.File.Data : null);
            var dto = MapFileRules.ToDto(file);
            dto.UploadedByName = viewer.User.FullName;
            return dto;
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
