using System.Text.Json;
using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using static Application.Features.MapLayers.MapLayerRules;

namespace Application.Features.MapLayers.Layers
{
    /// <summary>الدوال المشتركة بين عمليات هذه الميزة</summary>
    public class MapLayerSupport
    {
        private readonly IMapLayerService _layers;
        private readonly IMapEntryService _entries;
        private readonly IMapLayerSettings _settings;
        private readonly IEnumerable<IMapLayerProvider> _providers;

        public MapLayerSupport(IMapLayerService layers, IMapEntryService entries, IMapLayerSettings settings, IEnumerable<IMapLayerProvider> providers)
        {
            _layers = layers;
            _entries = entries;
            _settings = settings;
            _providers = providers;
        }

        public async Task<MapLayer> LoadAsync(int id, Viewer viewer)
        {
            var layer = await _layers.GetByIdAsync(id) ?? throw new KeyNotFoundException("الطبقة غير موجودة");
            EnsureLayer(viewer, layer);
            return layer;
        }

        public async Task<MapLayerDefinitionDto> DtoAsync(MapLayer layer) =>
            ToDto(layer, await _entries.CountForLayerAsync(layer.Id), includeHidden: true, SourceTitle(layer.Source));

        public string SourceTitle(string source) =>
            source.Length == 0 ? string.Empty : _providers.FirstOrDefault(p => p.Key == source)?.Title ?? source;

        /// <summary>مصدر الطبقة: فارغ = يدوي، وإلا مزوّد معروف</summary>
        public IMapLayerProvider? ProviderFor(string? source)
        {
            var key = (source ?? string.Empty).Trim();
            if (key.Length == 0) return null;
            return _providers.FirstOrDefault(p => p.Key == key) ?? throw new InvalidOperationException("مصدر الطبقة غير معروف");
        }

        /// <summary>فلتر المزوّد نظيفاً كـ JSON (لا يتجاوز طول العمود)</summary>
        public static string FilterJson(IMapLayerProvider provider, IReadOnlyDictionary<string, List<string>>? filter)
        {
            var json = JsonSerializer.Serialize(CleanFilter(filter, provider.FilterKeys));
            if (json.Length > 2000) throw new InvalidOperationException("الفلتر طويل جداً — قلّل الخيارات المختارة");
            return json;
        }

        public static MapLayerField NewField(MapLayerFieldRequestDto dto, int order)
        {
            var field = new MapLayerField
            {
                Label = dto.Label.Trim(),
                Type = (MapFieldType)dto.Type,
                IsRequired = dto.IsRequired,
                Options = JsonSerializer.Serialize(dto.Type == (int)MapFieldType.Select ? CleanOptions(dto.Options) : []),
                SortOrder = order
            };
            ApplyFileSettings(field, dto);
            return field;
        }

        /// <summary>إعدادات حقل الملفات (العدد والحجم والصيغ)؛ تُصفَّر لغيره</summary>
        public static void ApplyFileSettings(MapLayerField field, MapLayerFieldRequestDto dto)
        {
            var isFiles = dto.Type == (int)MapFieldType.Files;
            field.MaxFiles = isFiles ? dto.MaxFiles : 0;
            field.MaxFileMegabytes = isFiles ? dto.MaxFileMegabytes : 0;
            field.FileKinds = JsonSerializer.Serialize(isFiles ? MapFileRules.CleanKinds(dto.FileKinds) : []);
        }

        /// <summary>حجم الملف في أي حقل لا يتجاوز سقف النظام (إعدادات الخادم)</summary>
        public void EnsureFileLimits(IEnumerable<MapLayerFieldRequestDto> fields)
        {
            var over = fields.FirstOrDefault(f => f.Type == (int)MapFieldType.Files && f.MaxFileMegabytes > _settings.MaxFileMegabytes);
            if (over != null)
                throw new InvalidOperationException($"حجم الملف في «{over.Label.Trim()}» لا يتجاوز سقف النظام {_settings.MaxFileMegabytes}MB");
        }

        public async Task<MapLayerField> LoadFieldAsync(int fieldId, Viewer viewer)
        {
            var field = await _layers.GetFieldAsync(fieldId) ?? throw new KeyNotFoundException("الحقل غير موجود");
            EnsureLayer(viewer, field.Layer);
            return field;
        }
    }
}
