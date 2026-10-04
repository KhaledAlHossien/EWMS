using System.Reflection;
using System.Text.Json;

namespace Application.Common
{
    /// <summary>
    /// المحافظات السورية الأربع عشرة — "المناطق" الثابتة لمواقع توثيق الأجهزة (قرار المستخدم 2026-10-03:
    /// لا جدول مناطق؛ تُحدَّد المحافظة تلقائياً من إحداثيات الموقع). الحدود من ملف GeoJSON نفسه الذي تعرضه الخريطة
    /// في الواجهة (public/maps/syria-governorates.geojson، OCHA/HDX CC BY-IGO) ومنسوخ هنا كمورد مضمّن —
    /// عدّلهما معاً إن تغيّر الملف. الرموز SYxx هي رموز المحافظات في الملف.
    /// </summary>
    public static class Governorates
    {
        public sealed record Governorate(string Code, string NameAr);

        private static readonly Lazy<IReadOnlyList<(Governorate Governorate, List<List<List<double[]>>> Polygons)>> Loaded = new(Load);

        public static IReadOnlyList<Governorate> All => Loaded.Value.Select(x => x.Governorate).ToList();

        public static bool IsValidCode(string? code) =>
            !string.IsNullOrEmpty(code) && Loaded.Value.Any(x => x.Governorate.Code == code);

        public static string NameOf(string? code) =>
            Loaded.Value.FirstOrDefault(x => x.Governorate.Code == code).Governorate?.NameAr ?? string.Empty;

        /// <summary>
        /// أقصى بُعد (بالدرجات ≈ 5.5 كم) يُقبل بين النقطة وحدّ محافظة: الحدود المبسّطة تقصّ الساحل والحدود قليلاً،
        /// فمدينة ساحلية (طرطوس، اللاذقية، بانياس…) قد تقع نقطتها خارج المضلع بعدة مئات من الأمتار.
        /// مطابق لـ NEAR_BORDER_TOLERANCE في core/utils/geo.ts.
        /// </summary>
        public const double BorderTolerance = 0.05;

        /// <summary>المحافظة التي تقع فيها النقطة، أو أقربها إن كانت على بُعد ≤ BorderTolerance من حدّها؛ وإلا null (بحر/خارج سوريا)</summary>
        public static Governorate? Locate(double latitude, double longitude)
        {
            foreach (var (governorate, polygons) in Loaded.Value)
                if (polygons.Any(rings => InPolygon(latitude, longitude, rings)))
                    return governorate;

            Governorate? nearest = null;
            var best = BorderTolerance;
            foreach (var (governorate, polygons) in Loaded.Value)
                foreach (var rings in polygons)
                    foreach (var ring in rings)
                    {
                        var distance = DistanceToRing(latitude, longitude, ring);
                        if (distance <= best) { best = distance; nearest = governorate; }
                    }
            return nearest;
        }

        // أقرب مسافة (بالدرجات، مستوٍ تقريبي) من النقطة إلى حلقة
        private static double DistanceToRing(double lat, double lng, List<double[]> ring)
        {
            var best = double.MaxValue;
            for (var i = 0; i < ring.Count - 1; i++)
            {
                double ax = ring[i][0], ay = ring[i][1], bx = ring[i + 1][0], by = ring[i + 1][1];
                double dx = bx - ax, dy = by - ay;
                var length2 = dx * dx + dy * dy;
                var t = length2 == 0 ? 0 : Math.Clamp(((lng - ax) * dx + (lat - ay) * dy) / length2, 0, 1);
                best = Math.Min(best, Math.Sqrt(Math.Pow(lng - (ax + t * dx), 2) + Math.Pow(lat - (ay + t * dy), 2)));
            }
            return best;
        }

        // ──────────────── التحميل ────────────────
        private static IReadOnlyList<(Governorate, List<List<List<double[]>>>)> Load()
        {
            using var stream = typeof(Governorates).Assembly.GetManifestResourceStream("syria-governorates.geojson")
                ?? throw new InvalidOperationException("ملف حدود المحافظات غير موجود ضمن موارد المشروع");
            using var doc = JsonDocument.Parse(stream);

            var result = new List<(Governorate, List<List<List<double[]>>>)>();
            foreach (var feature in doc.RootElement.GetProperty("features").EnumerateArray())
            {
                var props = feature.GetProperty("properties");
                var governorate = new Governorate(props.GetProperty("code").GetString()!, props.GetProperty("nameAr").GetString()!);

                var geometry = feature.GetProperty("geometry");
                var coordinates = geometry.GetProperty("coordinates");
                var polygons = new List<List<List<double[]>>>();
                if (geometry.GetProperty("type").GetString() == "Polygon") polygons.Add(ReadPolygon(coordinates));
                else foreach (var polygon in coordinates.EnumerateArray()) polygons.Add(ReadPolygon(polygon));

                result.Add((governorate, polygons));
            }
            return result;
        }

        private static List<List<double[]>> ReadPolygon(JsonElement rings) =>
            rings.EnumerateArray()
                .Select(ring => ring.EnumerateArray().Select(p => new[] { p[0].GetDouble(), p[1].GetDouble() /* [lng, lat] */ }).ToList())
                .ToList();

        // ──────────────── Ray casting (مطابق لـ core/utils/geo.ts في الواجهة) ────────────────
        private static bool InRing(double lat, double lng, List<double[]> ring)
        {
            var inside = false;
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                double xi = ring[i][0], yi = ring[i][1], xj = ring[j][0], yj = ring[j][1];
                if ((yi > lat) != (yj > lat) && lng < (xj - xi) * (lat - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }

        private static bool InPolygon(double lat, double lng, List<List<double[]>> rings) =>
            rings.Count > 0 && InRing(lat, lng, rings[0]) && !rings.Skip(1).Any(hole => InRing(lat, lng, hole));
    }
}
