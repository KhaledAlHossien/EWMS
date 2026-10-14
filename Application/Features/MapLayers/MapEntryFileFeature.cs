using System.Globalization;
using System.Text.Json;
using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.MapLayers.MapLayerRules;

namespace Application.Features.MapLayers
{
    public record GetMapFileOptionsQuery : IRequest<MapFileOptionsDto>;
    /// <summary>رفع ملف لحقل «ملفات» — يبقى معلّقاً حتى يُحفظ السجل بمعرّفه في قيمة الحقل</summary>
    public record UploadMapEntryFileCommand(int FieldId, UploadedFileDto File) : IRequest<MapEntryFileDto>;
    public record GetMapEntryFileQuery(int Id) : IRequest<MapEntryFileContentDto>;
    /// <summary>حذف ملف معلّق رفعه المستخدم ولم يحفظه (إلغاء النموذج أو إزالته قبل الحفظ)</summary>
    public record DeletePendingMapEntryFileCommand(int Id) : IRequest<Unit>;

    public class MapEntryFileHandler :
        IRequestHandler<GetMapFileOptionsQuery, MapFileOptionsDto>,
        IRequestHandler<UploadMapEntryFileCommand, MapEntryFileDto>,
        IRequestHandler<GetMapEntryFileQuery, MapEntryFileContentDto>,
        IRequestHandler<DeletePendingMapEntryFileCommand, Unit>
    {
        private readonly IMapEntryFileService _files;
        private readonly IMapLayerService _layers;
        private readonly IMapLayerSettings _settings;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly IFileStore _store;

        public MapEntryFileHandler(IMapEntryFileService files, IMapLayerService layers, IMapLayerSettings settings,
            IUserService users, IUserPermissionService permissions, IFileStore store)
        {
            _store = store;
            _files = files;
            _layers = layers;
            _settings = settings;
            _users = users;
            _permissions = permissions;
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);

        public Task<MapFileOptionsDto> Handle(GetMapFileOptionsQuery request, CancellationToken ct) => Task.FromResult(new MapFileOptionsDto
        {
            MaxFileMegabytes = _settings.MaxFileMegabytes,
            MaxFilesPerField = MapFileRules.MaxFilesPerField,
            Kinds = MapFileRules.Kinds.Select(k => new MapFileKindDto { Key = k.Key, Label = k.Label, Formats = k.Formats, Extensions = [.. k.Extensions] }).ToList()
        });

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

        public async Task<Unit> Handle(DeletePendingMapEntryFileCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var file = await _files.GetByIdAsync(request.Id) ?? throw new KeyNotFoundException("الملف غير موجود");
            if (file.EntryId != null) throw new InvalidOperationException("الملف مرتبط بسجل — أزله من نموذج السجل ثم احفظ");
            if (file.UploadedByUserId != viewer.Id) throw new UnauthorizedAccessException("لا يمكنك حذف هذا الملف");
            await _files.DeleteAsync(file);
            return Unit.Value;
        }
    }

    /// <summary>ربط ملفات حقول «ملفات» بالسجل عند حفظه، وحذف الملفات المعلّقة القديمة</summary>
    public static class MapEntryFiles
    {
        /// <summary>
        /// قيمة حقل الملفات في الطلب = قائمته الكاملة بعد الحفظ (معرّفات معلّقة رفعها المستخدم لهذا الحقل، أو مرتبطة بهذا السجل).
        /// الحقل الغائب عن الطلب عند التعديل يبقى كما هو؛ الحقول المخفية لا تُمسّ. يتحقق من «مطلوب» ومن أقصى عدد.
        /// </summary>
        public static async Task<MapEntryFileChanges> PlanAsync(MapLayer layer, MapEntry? entry,
            IReadOnlyDictionary<string, JsonElement> input, Viewer viewer, IMapEntryFileService files)
        {
            var fields = layer.Fields.Where(f => f.Type == MapFieldType.Files && !f.IsHidden).ToList();
            if (fields.Count == 0) return MapEntryFileChanges.None;

            var requested = new Dictionary<int, List<int>>();
            foreach (var field in fields)
                if (input.TryGetValue(field.Id.ToString(CultureInfo.InvariantCulture), out var raw))
                    requested[field.Id] = ParseIds(raw, field);

            var existing = entry?.Files.ToList() ?? [];
            var pendingIds = requested.Values.SelectMany(ids => ids).Distinct().Where(id => existing.All(f => f.Id != id)).ToList();
            var pending = pendingIds.Count > 0 ? await files.GetByIdsAsync(pendingIds) : [];

            var attach = new List<MapEntryFile>();
            var remove = new List<MapEntryFile>();
            foreach (var field in fields)
            {
                var current = existing.Where(f => f.FieldId == field.Id).ToList();
                if (!requested.TryGetValue(field.Id, out var ids))
                {
                    if (field.IsRequired && current.Count == 0) throw new InvalidOperationException($"الحقل «{field.Label}» مطلوب — أرفق ملفاً واحداً على الأقل");
                    continue;
                }

                var final = new List<MapEntryFile>();
                foreach (var id in ids.Distinct())
                {
                    var file = current.FirstOrDefault(f => f.Id == id) ?? pending.FirstOrDefault(f => f.Id == id);
                    var valid = file != null && file.FieldId == field.Id
                        && (file.EntryId == null ? file.UploadedByUserId == viewer.Id : file.EntryId == entry?.Id);
                    if (!valid) throw new InvalidOperationException($"أحد ملفات «{field.Label}» غير موجود أو انتهت مهلته — أعد رفعه");
                    final.Add(file!);
                }

                var added = final.Where(f => f.EntryId == null).ToList();
                if (added.Count > 0 && final.Count > field.MaxFiles)
                    throw new InvalidOperationException($"الحقل «{field.Label}» يقبل {field.MaxFiles} ملفات كحد أقصى");
                if (field.IsRequired && final.Count == 0)
                    throw new InvalidOperationException($"الحقل «{field.Label}» مطلوب — أرفق ملفاً واحداً على الأقل");

                attach.AddRange(added);
                remove.AddRange(current.Where(f => !final.Contains(f)));
            }
            return new MapEntryFileChanges(attach, remove);
        }

        private static List<int> ParseIds(JsonElement raw, MapLayerField field)
        {
            if (raw.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return [];
            if (raw.ValueKind != JsonValueKind.Array) throw new InvalidOperationException($"قيمة الحقل «{field.Label}» غير صحيحة");
            var ids = new List<int>();
            foreach (var item in raw.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out var id))
                    throw new InvalidOperationException($"قيمة الحقل «{field.Label}» غير صحيحة");
                ids.Add(id);
            }
            return ids;
        }

        /// <summary>تُستدعى من خدمة الخلفية: المعلّق أكثر من 24 ساعة لم يُحفظ سجله (أُغلق النموذج) فيُحذف</summary>
        public static Task<int> CleanupAsync(IMapEntryFileService files, DateTime utcNow) =>
            files.DeletePendingBeforeAsync(utcNow.AddHours(-24));
    }
}
