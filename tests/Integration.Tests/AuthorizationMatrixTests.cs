using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Common;
using Application.DTOs.Response;
using Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Integration.Tests;

/// <summary>
/// مصفوفة الصلاحيات الحيّة: لا تعتمد على أن الشيفرة «تبدو صحيحة» بل تجرّب كل نقطة نهاية فعلاً ضد التطبيق الحقيقي.
/// القائمة تُولَّد من المتحكمات نفسها (لا جدول يدوي يتقادم). لكل نقطة:
///   بلا هوية ← 401؛ بدور فارغ ← رفض السياسة؛ بدور يملك ما تحتاجه وحده ← تمرّ من بوابة الصلاحية؛
///   وبدور يملك كل شيء عدا إحدى صلاحياتها ← تُرفض. فالسياسة المكتوبة هي المطبَّقة فعلاً، وصلاحية أخرى لا تفتحها بالخطأ.
/// سياسات المتحكم والدالة تُطبَّق معاً (الاجتياز يحتاج كلتيهما)، فالمطلوب هو اجتماعهما.
/// تعمل في حاوية خاصة لأن بعض النداءات (بمعرّفات غير موجودة وأجسام فارغة) قد تنشئ سجلاً هامشياً.
/// </summary>
[Collection(MatrixCollection.Name)]
public partial class AuthorizationMatrixTests(ApiFixture api)
{
    private const string PolicyDenied = "ليس لديك صلاحية لهذا الإجراء";

    private sealed partial record Endpoint(HttpMethod Method, string Template, string Name, IReadOnlyList<string> Policies, bool Form)
    {
        public string Label => $"{Method} {Template}";
        public string PolicyText => string.Join(" + ", Policies);

        /// <summary>يملأ متغيرات المسار بقيم لا وجود لها: لا يُعدَّل أو يُحذف سجل حقيقي بالخطأ</summary>
        public string Path => PathParameter().Replace(Template, m =>
            m.Groups["c"].Value == "guid" ? Guid.NewGuid().ToString() : m.Groups["n"].Value == "key" ? "unknown" : "999999");

        [GeneratedRegex(@"\{(?<n>[^}:?]+)(?::(?<c>[^}?]+))?\??\}")]
        private static partial Regex PathParameter();
    }

    private static List<Endpoint> Endpoints() =>
        typeof(Program).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
                .Select(m =>
                {
                    var verb = m.GetCustomAttributes<HttpMethodAttribute>().First();
                    var name = c.Name.Replace("Controller", string.Empty);
                    var prefix = (c.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty).Replace("[controller]", name);
                    var template = "/" + (prefix + (string.IsNullOrEmpty(verb.Template) ? string.Empty : "/" + verb.Template)).TrimStart('/');
                    var policies = c.GetCustomAttributes<AuthorizeAttribute>().Concat(m.GetCustomAttributes<AuthorizeAttribute>())
                        .Select(a => a.Policy).Where(p => p != null).Select(p => p!).Distinct().ToList();
                    // نقاط تستقبل نموذجاً/ملفات: تُرسل بنموذج فارغ لا JSON (وإلا ردّ التوجيه 415 قبل أن تُفحص الصلاحية)
                    var form = m.GetCustomAttributes<ConsumesAttribute>().Any(a => a.ContentTypes.Any(x => x.Contains("multipart")))
                               || m.GetParameters().Any(x => x.GetCustomAttributes<FromFormAttribute>().Any() || x.ParameterType == typeof(IFormFile));
                    return new Endpoint(new HttpMethod(verb.HttpMethods.First()), template, $"{name}.{m.Name}", policies, form);
                }))
            .OrderBy(e => e.Label, StringComparer.Ordinal).ToList();

    /// <summary>
    /// رفض بوابة الصلاحية: 403 برسالتها الثابتة (تُفكّ من JSON لأن العربية فيه مشفّرة \uXXXX).
    /// أما 403 المعالج («هذا السجل خارج صلاحياتك») فبرسالة أخرى وهي ليست رفض سياسة.
    /// </summary>
    private static bool IsPolicyDenial((HttpStatusCode Status, string Body) r)
    {
        if (r.Status != HttpStatusCode.Forbidden) return false;
        try { return JsonDocument.Parse(r.Body).RootElement.GetProperty("message").GetString() == PolicyDenied; }
        catch (Exception) { return false; }
    }

    /// <summary>الصلاحيات التي تفتح السياسة: هي نفسها إن كانت صلاحية، وأعضاؤها إن كانت سياسة «أيّ من»</summary>
    private static string[] Grants(string policy) => AppPermissions.AnyOf.TryGetValue(policy, out var members) ? members : [policy];

    private sealed record Actor(ApiClient Client, int RoleId);

    /// <summary>مستخدم بدور بلا صلاحيات يُعدَّل لاحقاً حسب الحالة</summary>
    private async Task<Actor> NewActorAsync(string label)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var role = (await api.Admin.PostForm<RoleResponseDto>("/api/Roles/Create", [new("Name", $"مصفوفة {label} {suffix}")])).Ok();
        var email = $"matrix-{label}-{suffix}@test.sy";
        (await api.Admin.PostForm<UserResponseDto>("/api/Users/Create", [
            new("FullName", $"مصفوفة {label}"), new("Email", email), new("Password", ApiFixture.Password), new("RoleId", role.Id.ToString()),
            new("BranchId", api.BranchId.ToString()), new("DepartmentId", api.DepartmentId.ToString())])).Ok();
        return new Actor(await api.LoginAsync(email, ApiFixture.Password), role.Id);
    }

    private async Task GrantAsync(Actor actor, IEnumerable<int> permissionIds) =>
        (await api.Admin.PutForm<RoleResponseDto>($"/api/Roles/Update/{actor.RoleId}",
            [new("Name", $"دور مصفوفة {actor.RoleId}"), .. permissionIds.Select(id => new KeyValuePair<string, string>("PermissionIds", id.ToString()))])).Ok();

    [Fact]
    public async Task Without_a_token_every_endpoint_except_login_answers_401()
    {
        var anonymous = api.Anonymous();
        var wrong = new List<string>();
        foreach (var endpoint in Endpoints().Where(e => e.Name != "Auth.Login"))
        {
            var result = await anonymous.Probe(endpoint.Method, endpoint.Path, endpoint.Form);
            if (result.Status != HttpStatusCode.Unauthorized) wrong.Add($"{endpoint.Label} → {(int)result.Status}");
        }
        Assert.True(wrong.Count == 0, $"نقاط نهاية لا ترفض غير المصادَق بـ 401:\n{Lines(wrong)}");
    }

    [Fact]
    public async Task A_user_whose_role_has_no_permissions_is_refused_by_every_policy_protected_endpoint()
    {
        var actor = await NewActorAsync("empty");
        var wrong = new List<string>();
        foreach (var endpoint in Endpoints().Where(e => e.Policies.Count > 0))
        {
            var result = await actor.Client.Probe(endpoint.Method, endpoint.Path, endpoint.Form);
            if (!IsPolicyDenial(result)) wrong.Add($"{endpoint.Label} [{endpoint.PolicyText}] → {(int)result.Status}");
        }
        Assert.True(wrong.Count == 0, $"نقاط نهاية خلف سياسة لكنها لا ترفض صاحب دور فارغ برفض السياسة:\n{Lines(wrong)}");
    }

    /// <summary>
    /// «يفتح»: دور يملك صلاحية واحدة من كل سياسة مطلوبة (وكل عضو من أعضاء سياسة «أيّ من» بالتناوب) فتمرّ النقطة من بوابة الصلاحية.
    /// «يُغلق»: دور يملك كل شيء عدا صلاحيات إحدى السياسات فتُرفض النقطة برفض السياسة.
    /// المجموعات المتطابقة تُجمَّع كي يُعدَّل الدور مرة واحدة لكل مجموعة.
    /// </summary>
    [Fact]
    public async Task Each_policy_opens_for_its_own_permission_alone_and_closes_when_only_that_permission_is_missing()
    {
        var actor = await NewActorAsync("single");
        var permissions = (await api.Admin.Get<List<PermissionResponseDto>>("/api/Roles/Permissions")).Ok().ToDictionary(p => p.Name, p => p.Id);
        var protectedEndpoints = Endpoints().Where(e => e.Policies.Count > 0).ToList();

        var opens = new Dictionary<string, (HashSet<string> Granted, List<Endpoint> Endpoints)>();
        var closes = new Dictionary<string, (HashSet<string> Granted, List<Endpoint> Endpoints)>();
        static void Add(Dictionary<string, (HashSet<string> Granted, List<Endpoint> Endpoints)> map, HashSet<string> granted, Endpoint endpoint)
        {
            var key = string.Join(",", granted.Order(StringComparer.Ordinal));
            if (!map.TryGetValue(key, out var entry)) map[key] = entry = (granted, []);
            entry.Endpoints.Add(endpoint);
        }

        foreach (var endpoint in protectedEndpoints)
            foreach (var policy in endpoint.Policies)
            {
                foreach (var member in Grants(policy))
                    Add(opens, [member, .. endpoint.Policies.Where(q => q != policy).Select(q => Grants(q)[0])], endpoint);

                var excluded = Grants(policy).ToHashSet();
                Add(closes, permissions.Keys.Where(name => !excluded.Contains(name)).ToHashSet(), endpoint);
            }

        var notOpened = new List<string>();
        var notClosed = new List<string>();
        var crashed = new List<string>();

        foreach (var (granted, endpoints) in opens.Values)
        {
            await GrantAsync(actor, granted.Select(name => permissions[name]));
            foreach (var endpoint in endpoints)
            {
                var result = await actor.Client.Probe(endpoint.Method, endpoint.Path, endpoint.Form);
                if (result.Status == HttpStatusCode.Unauthorized || IsPolicyDenial(result))
                    notOpened.Add($"{endpoint.Label} [{endpoint.PolicyText}] لم تفتحها ({string.Join("، ", granted)}) → {(int)result.Status}");
                if ((int)result.Status >= 500) crashed.Add($"{endpoint.Label} ({string.Join("، ", granted)}) → {(int)result.Status}");
            }
        }

        foreach (var (granted, endpoints) in closes.Values)
        {
            await GrantAsync(actor, granted.Select(name => permissions[name]));
            foreach (var endpoint in endpoints)
            {
                var result = await actor.Client.Probe(endpoint.Method, endpoint.Path, endpoint.Form);
                if (!IsPolicyDenial(result)) notClosed.Add($"{endpoint.Label} [{endpoint.PolicyText}] فُتحت وإحدى صلاحياتها ناقصة → {(int)result.Status}");
            }
        }

        Assert.True(notOpened.Count == 0, $"صلاحية لا تفتح نقطتها:\n{Lines(notOpened)}");
        Assert.True(notClosed.Count == 0, $"صلاحية أخرى تفتح النقطة أو السياسة لا تُطبَّق:\n{Lines(notClosed.Distinct())}");
        Assert.True(crashed.Count == 0, $"خطأ خادم (5xx) عند فتح البوابة بمدخلات فارغة، يجب أن تردّ 4xx:\n{Lines(crashed.Distinct())}");
    }

    private static string Lines(IEnumerable<string> items) => string.Join(Environment.NewLine, items.Order().Select(i => "  - " + i));
}

/// <summary>مجموعة منفصلة (حاوية خاصة بها): المصفوفة تنفّذ نداءات كثيرة ولا تتشارك قاعدة بيانات مع بقية الاختبارات</summary>
[CollectionDefinition(Name)]
public sealed class MatrixCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "matrix";
}
