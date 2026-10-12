using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;

namespace Application.Features.MapLayers
{
    /// <summary>
    /// طبقات خرائط الفروع (1.2.0، قرارات المستخدم 2026-10-11) — مصدر واحد للحدود والتحقق والتحويل:
    /// - الطبقة وحقولها ملك الفرع ويستعملها كل أقسامه: يراها ويديرها موظفو فرعها (حسب صلاحياتهم)، والكل لنظرة المؤسسة.
    /// - السجل يحمل قسم من أدخله وفرع الطبقة: الحد = سجلات قسمي، أو فرعي كله مع ViewBranchMapEntries، أو الكل لنظرة المؤسسة.
    /// - القيم JSON بمفاتيح معرّفات الحقول، يتحقق منها الخادم حسب نوع كل حقل.
    /// </summary>
    public static partial class MapLayerRules
    {
        public const int MaxFields = 40;
        public const int MaxOptions = 50;
        public const int TextMax = 500;
        public const int LongTextMax = 4000;

        [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
        private static partial Regex ColorPattern();

        public static bool IsColor(string? color) => color != null && ColorPattern().IsMatch(color);

        // ───────── الحدود ─────────

        public static EntryScope EntryScopeFor(Viewer viewer)
        {
            if (viewer.IsOrganizationWide) return EntryScope.Everything;
            if (viewer.Has(AppPermissions.ViewBranchMapEntries) && viewer.User.BranchId is int branchId)
                return new EntryScope(false, branchId, null);
            if (viewer.User.DepartmentId is int departmentId)
                return new EntryScope(false, null, departmentId);
            return EntryScope.Nothing;
        }

        /// <summary>طبقات فرعي (أو أي فرع لنظرة المؤسسة)</summary>
        public static bool CanAccessLayer(Viewer viewer, MapLayer layer) =>
            viewer.IsOrganizationWide || OrganizationRole.InBranch(viewer.User, layer.BranchId);

        public static void EnsureLayer(Viewer viewer, MapLayer layer)
        {
            if (!CanAccessLayer(viewer, layer))
                throw new UnauthorizedAccessException("هذه الطبقة تتبع فرعاً آخر");
        }

        public static void EnsureEntry(Viewer viewer, MapEntry entry)
        {
            if (!EntryScopeFor(viewer).Includes(entry))
                throw new UnauthorizedAccessException("هذا السجل يتبع قسماً أو فرعاً آخر");
        }

        // ───────── الحقول ─────────

        public static List<string> CleanOptions(IEnumerable<string>? options) =>
            (options ?? []).Select(o => (o ?? string.Empty).Trim()).Where(o => o.Length > 0).Distinct().Take(MaxOptions).ToList();

        public static List<string> ParseOptions(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return [];
            try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
            catch (JsonException) { return []; }
        }

        // ───────── فلتر طبقات بيانات النظام ─────────

        public static Dictionary<string, List<string>> ParseFilter(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try { return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json) ?? new(); }
            catch (JsonException) { return new(); }
        }

        /// <summary>مفاتيح المزوّد المعروفة فقط، وقيم غير فارغة بلا تكرار (القائمة الفارغة = الكل فلا تُحفظ)</summary>
        public static Dictionary<string, List<string>> CleanFilter(IReadOnlyDictionary<string, List<string>>? filter, IReadOnlyCollection<string> keys) =>
            (filter ?? new Dictionary<string, List<string>>())
                .Where(kv => keys.Contains(kv.Key))
                .Select(kv => (kv.Key, Values: (kv.Value ?? []).Select(v => (v ?? string.Empty).Trim())
                    .Where(v => v.Length is > 0 and <= 200).Distinct().Take(100).ToList()))
                .Where(x => x.Values.Count > 0)
                .ToDictionary(x => x.Key, x => x.Values);

        public static Dictionary<string, JsonElement> ParseValues(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try { return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new(); }
            catch (JsonException) { return new(); }
        }

        /// <summary>
        /// يتحقق من القيم حسب حقول الطبقة ويعيدها JSON نظيفاً: المجهول يُهمل، والفارغ لا يُحفظ،
        /// والحقول المخفية تبقى قيمها السابقة كما هي (لا تظهر في النموذج فلا تُمحى).
        /// </summary>
        public static string NormalizeValues(MapLayer layer, IReadOnlyDictionary<string, JsonElement> input, string? existingJson)
        {
            var existing = ParseValues(existingJson);
            var result = new Dictionary<string, object?>();
            foreach (var field in layer.Fields.OrderBy(f => f.SortOrder).ThenBy(f => f.Id))
            {
                // حقل الملفات لا يُحفظ في JSON: ملفاته في MapEntryFiles (MapEntryFiles.PlanAsync)
                if (field.Type == MapFieldType.Files) continue;
                var key = field.Id.ToString(CultureInfo.InvariantCulture);
                if (field.IsHidden)
                {
                    if (existing.TryGetValue(key, out var old)) result[key] = old;
                    continue;
                }
                var value = input.TryGetValue(key, out var raw) ? Convert(field, raw) : null;
                if (value == null)
                {
                    if (field.IsRequired) throw new InvalidOperationException($"الحقل «{field.Label}» مطلوب");
                    continue;
                }
                result[key] = value;
            }
            return JsonSerializer.Serialize(result);
        }

        private static object? Convert(MapLayerField field, JsonElement raw)
        {
            if (raw.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
            var text = raw.ValueKind switch
            {
                JsonValueKind.String => raw.GetString()!.Trim(),
                JsonValueKind.Number => raw.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => throw new InvalidOperationException($"قيمة الحقل «{field.Label}» غير صحيحة")
            };
            if (text.Length == 0) return null;

            switch (field.Type)
            {
                case MapFieldType.Text:
                    if (text.Length > TextMax) throw new InvalidOperationException($"الحقل «{field.Label}» لا يتجاوز {TextMax} حرف");
                    return text;
                case MapFieldType.LongText:
                    if (text.Length > LongTextMax) throw new InvalidOperationException($"الحقل «{field.Label}» لا يتجاوز {LongTextMax} حرف");
                    return text;
                case MapFieldType.Number:
                    var normalized = text.Replace('٫', '.').Replace(',', '.');
                    if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                        throw new InvalidOperationException($"الحقل «{field.Label}» يقبل رقماً فقط");
                    return number;
                case MapFieldType.Date:
                    if (!DateTime.TryParseExact(text.Length >= 10 ? text[..10] : text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                        throw new InvalidOperationException($"الحقل «{field.Label}» يقبل تاريخاً (مثال 2026-10-11)");
                    return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                case MapFieldType.Select:
                    var options = ParseOptions(field.Options);
                    if (!options.Contains(text)) throw new InvalidOperationException($"اختر قيمة الحقل «{field.Label}» من القائمة");
                    return text;
                case MapFieldType.YesNo:
                    return text switch
                    {
                        "true" or "1" or "نعم" => true,
                        "false" or "0" or "لا" => false,
                        _ => throw new InvalidOperationException($"الحقل «{field.Label}» نعم أو لا")
                    };
                default:
                    throw new InvalidOperationException($"نوع الحقل «{field.Label}» غير معروف");
            }
        }

        // ───────── التحويل إلى DTO ─────────

        /// <summary>sourceTitle: اسم مزوّد بيانات النظام لطبقة من بياناته (فارغ للطبقة اليدوية)</summary>
        public static MapLayerDefinitionDto ToDto(MapLayer layer, int entriesCount, bool includeHidden, string sourceTitle = "") => new()
        {
            Id = layer.Id,
            BranchId = layer.BranchId,
            BranchName = layer.Branch?.Name ?? string.Empty,
            Name = layer.Name,
            Description = layer.Description,
            Color = layer.Color,
            AllowMultiple = layer.AllowMultiple,
            IsActive = layer.IsActive,
            SortOrder = layer.SortOrder,
            Source = layer.Source,
            SourceTitle = sourceTitle,
            SourceFilter = ParseFilter(layer.SourceFilter),
            EntriesCount = entriesCount,
            Fields = layer.Fields
                .Where(f => includeHidden || !f.IsHidden)
                .OrderBy(f => f.SortOrder).ThenBy(f => f.Id)
                .Select(f => new MapLayerFieldDto
                {
                    Id = f.Id, Label = f.Label, Type = (int)f.Type, IsRequired = f.IsRequired,
                    Options = ParseOptions(f.Options), SortOrder = f.SortOrder, IsHidden = f.IsHidden,
                    MaxFiles = f.MaxFiles, MaxFileMegabytes = f.MaxFileMegabytes, FileKinds = MapFileRules.ParseKinds(f.FileKinds)
                }).ToList(),
            RowVersion = layer.RowVersion
        };

        public static MapEntryDto ToDto(MapEntry entry, Viewer viewer) => new()
        {
            Id = entry.Id,
            LayerId = entry.LayerId,
            LayerName = entry.Layer?.Name ?? string.Empty,
            LayerColor = entry.Layer?.Color ?? string.Empty,
            SiteId = entry.SiteId,
            SiteName = entry.Site?.Name ?? string.Empty,
            BranchId = entry.BranchId,
            BranchName = entry.Branch?.Name ?? string.Empty,
            DepartmentId = entry.DepartmentId,
            DepartmentName = entry.Department?.Name ?? string.Empty,
            Values = ParseValues(entry.Values),
            Files = entry.Files.OrderBy(f => f.Id).Select(MapFileRules.ToDto).ToList(),
            CreatedByName = entry.CreatedByUser?.FullName ?? string.Empty,
            CreatedAt = entry.CreatedAt,
            UpdatedByName = entry.UpdatedByUser?.FullName ?? string.Empty,
            UpdatedAt = entry.UpdatedAt,
            // الطبقة المعطّلة تبقى سجلاتها للقراءة فقط
            CanEdit = viewer.Has("EditMapEntry") && entry.Layer?.IsActive == true,
            CanDelete = viewer.Has("DeleteMapEntry"),
            RowVersion = entry.RowVersion
        };
    }
}
