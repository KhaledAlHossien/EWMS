using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Domain.Entities;

namespace Application.Features.MapLayers
{
    /// <summary>
    /// حقل «ملفات» في طبقات الخرائط (1.2.0، قرارات المستخدم 2026-10-12): مدير الطبقة يختار لكل حقل مجموعات الصيغ
    /// وأقصى عدد ملفات وأقصى حجم (ضمن سقف النظام MapLayers:MaxFileMegabytes). النوع يُكشف من محتوى الملف لا من امتداده،
    /// والبرامج والسكربتات وHTML/SVG والمستندات ذات الماكرو ممنوعة دائماً. ما لا يُعاين (صورة/PDF) يُنزَّل فقط.
    /// </summary>
    public static class MapFileRules
    {
        public const int MaxFilesPerField = 20;
        public const int MaxFileNameLength = 200;
        /// <summary>حجم فحص النص (لا بايت صفري في أوله = ملف نصي)</summary>
        private const int TextProbeBytes = 64 * 1024;

        public sealed record FileKind(string Key, string Label, string Formats, string[] Extensions);

        /// <summary>مجموعات الصيغ — المفتاح يُحفظ في إعداد الحقل (FileKinds)، والواجهة تعرض الاسم والصيغ</summary>
        public static readonly IReadOnlyList<FileKind> Kinds =
        [
            new("images", "صور", "JPG, PNG, GIF, WEBP, BMP, TIFF", [".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tif", ".tiff"]),
            new("pdf", "PDF", "PDF", [".pdf"]),
            new("office", "مستندات Office", "DOCX, XLSX, PPTX, DOC, XLS, PPT, ODT, ODS, ODP", [".docx", ".xlsx", ".pptx", ".doc", ".xls", ".ppt", ".odt", ".ods", ".odp"]),
            new("text", "نصوص وبيانات", "TXT, CSV, JSON, XML", [".txt", ".csv", ".json", ".xml"]),
            new("cad", "مخططات هندسية", "DWG, DXF", [".dwg", ".dxf"]),
            new("maps", "ملفات خرائط", "KML, KMZ, GeoJSON, GPX, SHP مضغوط (ZIP)", [".kml", ".kmz", ".geojson", ".gpx", ".zip"]),
            new("archives", "أرشيف مضغوط", "ZIP, RAR, 7Z", [".zip", ".rar", ".7z"]),
            new("media", "فيديو وصوت", "MP4, MOV, MP3, WAV, M4A", [".mp4", ".mov", ".mp3", ".wav", ".m4a"]),
        ];

        /// <summary>ممنوعة دائماً، بالامتداد (وداخل ملفات ZIP): برامج وسكربتات وصفحات ومستندات بماكرو</summary>
        private static readonly HashSet<string> Blocked = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".msi", ".msp", ".com", ".scr", ".pif", ".cpl", ".sys", ".drv", ".bat", ".cmd", ".ps1", ".psm1", ".psd1",
            ".vbs", ".vbe", ".js", ".jse", ".mjs", ".wsf", ".wsh", ".hta", ".jar", ".apk", ".app", ".sh", ".bash", ".py", ".pl", ".rb",
            ".php", ".asp", ".aspx", ".jsp", ".cgi", ".html", ".htm", ".xhtml", ".mht", ".mhtml", ".svg", ".svgz", ".lnk", ".url", ".reg",
            ".inf", ".docm", ".dotm", ".xlsm", ".xltm", ".xlam", ".pptm", ".potm", ".ppam", ".sldm", ".iso", ".img", ".vhd", ".vhdx"
        };

        private static readonly HashSet<string> Previewable = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf", "image/jpeg", "image/png", "image/gif", "image/webp", "image/bmp"
        };

        /// <summary>صور يعرضها المتصفح وPDF تُعاين داخل الصفحة؛ غيرها يُنزَّل دائماً</summary>
        public static bool IsPreviewable(string contentType) => Previewable.Contains(contentType);

        public static List<string> CleanKinds(IEnumerable<string>? kinds) =>
            (kinds ?? []).Select(k => (k ?? string.Empty).Trim().ToLowerInvariant())
                .Where(k => Kinds.Any(x => x.Key == k)).Distinct().ToList();

        public static List<string> ParseKinds(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return [];
            try { return CleanKinds(JsonSerializer.Deserialize<List<string>>(json)); }
            catch (JsonException) { return []; }
        }

        public static string KindLabels(IEnumerable<string> keys) =>
            string.Join("، ", keys.Select(k => Kinds.FirstOrDefault(x => x.Key == k)?.Label ?? k));

        /// <summary>اسم الملف بلا مسار، ومقصوص إلى الطول المسموح مع الإبقاء على الامتداد</summary>
        public static string SafeFileName(string? name)
        {
            var file = Path.GetFileName((name ?? string.Empty).Replace('\\', '/').Split('/').Last()).Trim();
            if (string.IsNullOrEmpty(file)) file = "ملف";
            if (file.Length <= MaxFileNameLength) return file;
            var ext = Path.GetExtension(file);
            return file[..(MaxFileNameLength - ext.Length)] + ext;
        }

        /// <summary>نوع الملف ومجموعاته المحتملة (ملف SHP مضغوط = خرائط أو أرشيف)، أو null إن لم يكن مدعوماً</summary>
        public sealed record Detected(string ContentType, string[] Kinds);

        /// <summary>بداية الملف تطابق البصمة</summary>
        private static bool Sig(ReadOnlySpan<byte> data, params ReadOnlySpan<byte> signature) => data.StartsWith(signature);

        public static Detected? Detect(byte[] data, string? fileName)
        {
            using var stream = new MemoryStream(data, writable: false);
            return Detect(stream, fileName);
        }

        /// <summary>
        /// نفس الكشف على مجرى قابل للبحث (ملف كبير على القرص): يُقرأ منه أول 64KB فقط، وملفات ZIP تُفحص قائمة محتوياتها دون فكّها.
        /// لا يُغلق المجرى، ويتركه عند موضع غير محدد.
        /// </summary>
        public static Detected? Detect(Stream stream, string? fileName)
        {
            var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
            if (stream.Length == 0 || Blocked.Contains(ext)) return null;

            var head = new byte[(int)Math.Min(stream.Length, TextProbeBytes)];
            stream.Position = 0;
            stream.ReadExactly(head);
            var d = head.AsSpan();

            if (d.StartsWith("%PDF-"u8)) return new("application/pdf", ["pdf"]);
            if (Sig(d, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)) return new("image/png", ["images"]);
            if (Sig(d, 0xFF, 0xD8, 0xFF)) return new("image/jpeg", ["images"]);
            if (d.StartsWith("GIF87a"u8) || d.StartsWith("GIF89a"u8)) return new("image/gif", ["images"]);
            if (d.Length >= 12 && d.StartsWith("RIFF"u8) && d[8..12].SequenceEqual("WEBP"u8)) return new("image/webp", ["images"]);
            if (d.Length >= 12 && d.StartsWith("RIFF"u8) && d[8..12].SequenceEqual("WAVE"u8)) return new("audio/wav", ["media"]);
            if (d.StartsWith("BM"u8) && ext == ".bmp") return new("image/bmp", ["images"]);
            if (Sig(d, 0x49, 0x49, 0x2A, 0x00) || Sig(d, 0x4D, 0x4D, 0x00, 0x2A)) return new("image/tiff", ["images"]);

            // MP4 / MOV / M4A: صندوق ftyp — يُميَّز بالامتداد
            if (d.Length >= 12 && d[4..8].SequenceEqual("ftyp"u8))
                return ext switch
                {
                    ".mp4" => new("video/mp4", ["media"]),
                    ".mov" => new("video/quicktime", ["media"]),
                    ".m4a" => new("audio/mp4", ["media"]),
                    _ => null
                };
            if (ext == ".mp3" && (d.StartsWith("ID3"u8) || (d.Length >= 2 && d[0] == 0xFF && (d[1] & 0xE0) == 0xE0)))
                return new("audio/mpeg", ["media"]);

            if (d.StartsWith("AC1"u8) && ext == ".dwg") return new("image/vnd.dwg", ["cad"]);
            if (d.StartsWith("AutoCAD Binary DXF"u8)) return new("image/vnd.dxf", ["cad"]);

            if (d.StartsWith("Rar!\u001A\u0007"u8)) return ext == ".rar" ? new("application/vnd.rar", ["archives"]) : null;
            if (Sig(d, 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C)) return ext == ".7z" ? new("application/x-7z-compressed", ["archives"]) : null;

            // doc / xls / ppt القديمة (حاوية OLE): لا تُميَّز من البصمة، فنشترط امتداداً مطابقاً (msi أيضاً OLE وممنوع)
            if (Sig(d, 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1))
                return ext switch
                {
                    ".doc" => new("application/msword", ["office"]),
                    ".xls" => new("application/vnd.ms-excel", ["office"]),
                    ".ppt" => new("application/vnd.ms-powerpoint", ["office"]),
                    _ => null
                };

            if (Sig(d, 0x50, 0x4B, 0x03, 0x04) || Sig(d, 0x50, 0x4B, 0x05, 0x06)) return DetectZip(stream, ext);

            return DetectText(d, ext);
        }

        /// <summary>
        /// ملفات ZIP: Office الحديثة (بلا vbaProject)، OpenDocument، KMZ، SHP مضغوط، أو أرشيف ZIP عادي —
        /// ويُرفض أي ZIP فيه ملف ممنوع (برنامج أو سكربت). RAR و7Z لا تُفحص محتوياتها.
        /// </summary>
        private static Detected? DetectZip(Stream stream, string ext)
        {
            List<string> names;
            string? odfType = null;
            try
            {
                stream.Position = 0;
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
                names = zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList();
                var mimetype = zip.GetEntry("mimetype");
                if (mimetype is { Length: < 200 })
                {
                    using var reader = new StreamReader(mimetype.Open(), Encoding.ASCII);
                    odfType = reader.ReadToEnd().Trim();
                }
            }
            catch (Exception e) when (e is InvalidDataException or IOException or NotSupportedException)
            {
                return null;
            }

            if (names.Any(n => Blocked.Contains(Path.GetExtension(n)))) return null;

            if (names.Contains("[Content_Types].xml"))
            {
                if (names.Any(n => n.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase))) return null;
                if (names.Any(n => n.StartsWith("word/"))) return ext == ".docx" ? new("application/vnd.openxmlformats-officedocument.wordprocessingml.document", ["office"]) : null;
                if (names.Any(n => n.StartsWith("xl/"))) return ext == ".xlsx" ? new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ["office"]) : null;
                if (names.Any(n => n.StartsWith("ppt/"))) return ext == ".pptx" ? new("application/vnd.openxmlformats-officedocument.presentationml.presentation", ["office"]) : null;
                return null;
            }

            if (odfType != null)
                return (odfType, ext) switch
                {
                    ("application/vnd.oasis.opendocument.text", ".odt") => new(odfType, ["office"]),
                    ("application/vnd.oasis.opendocument.spreadsheet", ".ods") => new(odfType, ["office"]),
                    ("application/vnd.oasis.opendocument.presentation", ".odp") => new(odfType, ["office"]),
                    _ => null
                };

            if (ext == ".kmz") return names.Any(n => n.EndsWith(".kml", StringComparison.OrdinalIgnoreCase))
                ? new("application/vnd.google-earth.kmz", ["maps"]) : null;
            if (ext != ".zip") return null;
            return names.Any(n => n.EndsWith(".shp", StringComparison.OrdinalIgnoreCase))
                ? new("application/zip", ["maps", "archives"])
                : new("application/zip", ["archives"]);
        }

        /// <summary>ملفات نصية بالامتداد: لا بايت صفري في أولها (أو بداية UTF-16) — وتُنزَّل دائماً فلا تُشغَّل في المتصفح</summary>
        private static Detected? DetectText(ReadOnlySpan<byte> d, string ext)
        {
            var (type, kind) = ext switch
            {
                ".txt" => ("text/plain", "text"),
                ".csv" => ("text/csv", "text"),
                ".json" => ("application/json", "text"),
                ".xml" => ("application/xml", "text"),
                ".kml" => ("application/vnd.google-earth.kml+xml", "maps"),
                ".geojson" => ("application/geo+json", "maps"),
                ".gpx" => ("application/gpx+xml", "maps"),
                ".dxf" => ("image/vnd.dxf", "cad"),
                _ => (null, null)
            };
            if (type == null) return null;
            var utf16 = Sig(d, 0xFF, 0xFE) || Sig(d, 0xFE, 0xFF);
            if (!utf16 && d[..Math.Min(d.Length, TextProbeBytes)].IndexOf((byte)0) >= 0) return null;
            return new(type, [kind!]);
        }

        /// <summary>رسالة رفض الملف لهذا الحقل، أو null مع المجموعة المقبولة</summary>
        public static (string? Problem, Detected? Detected, string Kind) Check(UploadedFileDto file, MapLayerField field, int systemMaxMegabytes)
        {
            var name = SafeFileName(file.FileName);
            if (file.Data.Length == 0) return ($"الملف «{name}» فارغ", null, "");
            var fieldMb = field.MaxFileMegabytes > 0 ? field.MaxFileMegabytes : systemMaxMegabytes;
            var maxMb = Math.Min(fieldMb, systemMaxMegabytes);
            var limit = fieldMb <= systemMaxMegabytes ? $"حد الحقل «{field.Label}»" : "سقف النظام";
            if (file.Data.LongLength > maxMb * 1024L * 1024L) return ($"الملف «{name}» أكبر من {maxMb}MB ({limit})", null, "");

            var allowed = ParseKinds(field.FileKinds);
            var detected = Detect(file.Data, name);
            if (detected == null)
                return ($"الملف «{name}» نوعه غير مدعوم أو ممنوع — البرامج والسكربتات وصفحات HTML/SVG والمستندات ذات الماكرو لا تُقبل", null, "");
            var kind = detected.Kinds.FirstOrDefault(allowed.Contains);
            if (kind == null) return ($"الحقل «{field.Label}» يقبل: {KindLabels(allowed)}", null, "");
            return (null, detected, kind);
        }

        public static MapEntryFileDto ToDto(MapEntryFile f) => new()
        {
            Id = f.Id,
            FieldId = f.FieldId,
            FileName = f.FileName,
            ContentType = f.ContentType,
            Kind = f.Kind,
            Size = f.Size,
            Previewable = IsPreviewable(f.ContentType),
            UploadedByName = f.UploadedByUser?.FullName ?? string.Empty,
            UploadedAt = f.UploadedAt
        };
    }
}
