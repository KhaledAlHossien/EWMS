using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Integration.Tests.Infrastructure;

namespace Integration.Tests;

/// <summary>
/// عقد الـ API: وثيقة OpenAPI التي يولّدها التطبيق الحقيقي (مسارات، معاملات، أجسام، استجابات، ونماذج DTO) في لقطة ذهبية
/// contract/openapi.json تحت إدارة المصدر. أي تغيير في شكل واجهة يستهلكها الواجهة (حقل أُعيدت تسميته أو حُذف، نوع تغيّر،
/// نقطة جديدة) يظهر فرقاً في الـ commit فيراجعه إنسان، ثم تُولَّد أنواع TypeScript من اللقطة نفسها (npm run api:types في الواجهة).
/// تحديث اللقطة: UPDATE_API_CONTRACT=1 ثم تشغيل هذا الاختبار.
/// </summary>
[Collection(ApiCollection.Name)]
public class OpenApiContractTests(ApiFixture api)
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    [Fact]
    public async Task The_openapi_document_matches_the_reviewed_snapshot()
    {
        var response = await api.Anonymous().Probe(HttpMethod.Get, "/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.Status);

        var actual = Normalize(response.Body);
        var path = SourcePath();

        if (Environment.GetEnvironmentVariable("UPDATE_API_CONTRACT") == "1")
        {
            File.WriteAllText(path, actual);
            return;
        }

        var expected = File.ReadAllText(path).ReplaceLineEndings("\n");
        Assert.True(actual == expected,
            "تغيّرت وثيقة OpenAPI عن اللقطة المراجَعة (contract/openapi.json). راجع التغيير (هل يكسر الواجهة؟)، ثم حدّث اللقطة بتشغيل الاختبار مع UPDATE_API_CONTRACT=1، " +
            "وانسخها إلى الواجهة ووَلِّد الأنواع (npm run contract:sync && npm run api:types).\n" + FirstDifference(expected, actual));
    }

    [Fact]
    public async Task The_openapi_document_describes_the_whole_api()
    {
        var document = JsonNode.Parse((await api.Anonymous().Probe(HttpMethod.Get, "/swagger/v1/swagger.json")).Body)!;
        var paths = document["paths"]!.AsObject();

        Assert.True(paths.Count > 200, "الوثيقة بلا نقاط كافية: ربما عُطّل Swagger");
        Assert.NotNull(document["components"]?["schemas"]);
    }

    /// <summary>ترتيب ثابت للمفاتيح وبلا حقول متقلبة، فيصير الفرق الظاهر تغييراً حقيقياً فقط</summary>
    private static string Normalize(string json)
    {
        static JsonNode? Sort(JsonNode? node) => node switch
        {
            JsonObject o => new JsonObject(o.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => KeyValuePair.Create(p.Key, Sort(p.Value?.DeepClone())))),
            JsonArray a => new JsonArray(a.Select(x => Sort(x?.DeepClone())).ToArray()),
            _ => node?.DeepClone()
        };

        return Sort(JsonNode.Parse(json))!.ToJsonString(Pretty).ReplaceLineEndings("\n") + "\n";
    }

    private static string FirstDifference(string expected, string actual)
    {
        var a = expected.Split('\n');
        var b = actual.Split('\n');
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
            if (a[i] != b[i])
                return $"أول اختلاف عند السطر {i + 1}:\n  اللقطة: {a[i].Trim()}\n  الحالي: {b[i].Trim()}\n(السياق: {string.Join(" / ", b.Skip(Math.Max(0, i - 3)).Take(3).Select(x => x.Trim()))})";
        return $"طول مختلف: {a.Length} مقابل {b.Length} سطراً";
    }

    private static string SourcePath([CallerFilePath] string callerFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(callerFile)!, "..", "..", "contract", "openapi.json"));
}
