using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Integration.Tests.Infrastructure;

/// <summary>نتيجة نداء: الحالة والبيانات المقروءة والنص الخام (للرسالة عند الفشل)</summary>
public sealed record ApiResult<T>(HttpStatusCode Status, T? Data, string Raw)
{
    public bool IsSuccess => (int)Status is >= 200 and < 300;

    /// <summary>يُرجع البيانات أو يفشل الاختبار بنص الاستجابة كاملاً</summary>
    public T Ok()
    {
        if (!IsSuccess || Data is null) throw new Xunit.Sdk.XunitException($"توقّعت نجاحاً لكن الحالة {(int)Status}: {Raw}");
        return Data;
    }
}

/// <summary>عميل HTTP بهوية مستخدم واحد (يتحدث مع التطبيق الحقيقي في الذاكرة). كل نداء يمرّ من الوسائط والصلاحيات الفعلية.</summary>
public sealed class ApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public ApiClient(HttpClient http, int userId = 0, string? token = null)
    {
        _http = http;
        UserId = userId;
        if (token != null) _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public int UserId { get; }

    public Task<ApiResult<T>> Get<T>(string path) => Send<T>(new HttpRequestMessage(HttpMethod.Get, path));
    public Task<ApiResult<T>> Post<T>(string path, object? body = null) => Send<T>(Request(HttpMethod.Post, path, body));
    public Task<ApiResult<T>> Put<T>(string path, object? body = null) => Send<T>(Request(HttpMethod.Put, path, body));
    public Task<ApiResult<JsonElement>> Delete(string path) => Send<JsonElement>(new HttpRequestMessage(HttpMethod.Delete, path));

    /// <summary>الأدوار والمستخدمون والفروع تستقبل نموذجاً (multipart) لا JSON</summary>
    public Task<ApiResult<T>> PostForm<T>(string path, IEnumerable<KeyValuePair<string, string>> fields)
    {
        var form = new MultipartFormDataContent();
        foreach (var (key, value) in fields) form.Add(new StringContent(value), key);
        return Send<T>(new HttpRequestMessage(HttpMethod.Post, path) { Content = form });
    }

    public Task<ApiResult<T>> PutForm<T>(string path, IEnumerable<KeyValuePair<string, string>> fields)
    {
        var form = new MultipartFormDataContent();
        foreach (var (key, value) in fields) form.Add(new StringContent(value), key);
        return Send<T>(new HttpRequestMessage(HttpMethod.Put, path) { Content = form });
    }

    /// <summary>رفع ملفات (حقل files) بنموذج multipart</summary>
    public Task<ApiResult<T>> PostFiles<T>(string path, params (string Name, byte[] Data)[] files)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, data) in files) form.Add(new ByteArrayContent(data), "files", name);
        return Send<T>(new HttpRequestMessage(HttpMethod.Post, path) { Content = form });
    }

    /// <summary>نداء خام لمصفوفة الصلاحيات: الحالة والنص فقط، بجسم JSON فارغ للتغييرات</summary>
    public async Task<(HttpStatusCode Status, string Body)> Probe(HttpMethod method, string path, bool form = false)
    {
        using var request = new HttpRequestMessage(method, path);
        if (form) request.Content = new MultipartFormDataContent();
        else if (method != HttpMethod.Get && method != HttpMethod.Delete)
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    /// <summary>جزء من ملف مرفوع بالرفع المجزّأ (جسم خام)</summary>
    public Task<ApiResult<T>> PutBytes<T>(string path, byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return Send<T>(new HttpRequestMessage(HttpMethod.Put, path) { Content = content });
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, object? body) =>
        new(method, path) { Content = body is null ? null : JsonContent.Create(body, options: Json) };

    private async Task<ApiResult<T>> Send<T>(HttpRequestMessage request)
    {
        using var response = await _http.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();
        T? data = default;
        if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(raw))
            data = JsonSerializer.Deserialize<T>(raw, Json);
        return new ApiResult<T>(response.StatusCode, data, raw);
    }
}
