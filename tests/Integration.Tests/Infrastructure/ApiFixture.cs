using Application.DTOs.Response;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Testcontainers.MsSql;

namespace Integration.Tests.Infrastructure;

/// <summary>
/// بيئة الاختبار الكاملة، تُبنى مرة واحدة لكل التشغيل: SQL Server حقيقي في حاوية، والتطبيق نفسه (بالهجرات والبذر) فوقه،
/// ومؤسسة صغيرة جاهزة (فرعان وقسمان) وثلاثة مستخدمين بأدوارهم: معدّ، ومعتمد، وغريب من فرع آخر.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string Password = "Test@12345x";
    private const string AdminEmail = "admin@test.sy";
    private const string AdminPassword = "Admin@12345x";

    // الصلاحيات: المعدّ يُعدّ ويحدّث ولا يعتمد، والمعتمد يعتمد ولا يُعدّ، والغريب يقرأ ما يراه في فرعه فقط
    private static readonly string[] PreparerPermissions =
    [
        "ViewProjects", "CreateProject", "EditProject", "ViewProjectDocuments", "UploadProjectDocument", "DownloadProjectDocument", "ViewProjectSchedule",
        "EditProjectStartOrder", "EditProjectSchedule", "UpdateScheduleProgress", "ClassifyScheduleTask", "RecordScheduleDelay", "RequestScheduleExtension",
        "ViewDepartments", "ViewNotifications", "ViewProjectAudit"
    ];
    private static readonly string[] ApproverPermissions =
    [
        "ViewProjects", "ViewBranchProjects", "ViewProjectDocuments", "ViewProjectSchedule", "ApproveProjectSchedule", "ApproveDelayJustification",
        "ApproveScheduleExtension", "ViewDepartments", "ViewNotifications"
    ];
    private static readonly string[] OutsiderPermissions = ["ViewProjects", "ViewProjectSchedule", "ViewDepartments"];

    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private WebApplicationFactory<Program> _factory = null!;

    public ApiClient Admin { get; private set; } = null!;
    public ApiClient Preparer { get; private set; } = null!;
    public ApiClient Approver { get; private set; } = null!;
    public ApiClient Outsider { get; private set; } = null!;
    public int BranchId { get; private set; }
    public int DepartmentId { get; private set; }

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _sql.GetConnectionString(),
                ["JwtSettings:Key"] = "integration-tests-signing-key-0123456789-abcdef",
                ["DeviceInventory:PasswordKey"] = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + 7)).ToArray()),
                ["Seed:AdminEmail"] = AdminEmail,
                ["Seed:AdminPassword"] = AdminPassword,
                ["FileStorage:Mode"] = "Database",
                ["Swagger:Enabled"] = "false",
                ["Cors:AllowedOrigins:0"] = "https://localhost"
            }));
        });

        Admin = await LoginAsync(AdminEmail, AdminPassword);

        // المؤسسة: فرع الاختبار وقسمه، وفرع آخر وقسمه (للغريب)
        BranchId = await CreateBranchAsync("فرع الاختبار");
        DepartmentId = await CreateDepartmentAsync("قسم الاختبار", BranchId);
        var otherBranch = await CreateBranchAsync("فرع آخر");
        var otherDepartment = await CreateDepartmentAsync("قسم آخر", otherBranch);

        var permissions = (await Admin.Get<List<PermissionResponseDto>>("/api/Roles/Permissions")).Ok();
        var preparerRole = await CreateRoleAsync("معدّ الجدول", PreparerPermissions, permissions);
        var approverRole = await CreateRoleAsync("معتمد الجدول", ApproverPermissions, permissions);
        var outsiderRole = await CreateRoleAsync("غريب", OutsiderPermissions, permissions);

        await CreateUserAsync("معدّ الجدول", "preparer@test.sy", preparerRole, BranchId, DepartmentId);
        await CreateUserAsync("معتمد الجدول", "approver@test.sy", approverRole, BranchId, DepartmentId);
        await CreateUserAsync("غريب", "outsider@test.sy", outsiderRole, otherBranch, otherDepartment);

        Preparer = await LoginAsync("preparer@test.sy", Password);
        Approver = await LoginAsync("approver@test.sy", Password);
        Outsider = await LoginAsync("outsider@test.sy", Password);
    }

    /// <summary>عميل بلا هوية (لاختبار رفض غير المصادَق)</summary>
    public ApiClient Anonymous() => new(NewHttpClient());

    private HttpClient NewHttpClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    private async Task<ApiClient> LoginAsync(string email, string password)
    {
        var anonymous = new ApiClient(NewHttpClient());
        var login = (await anonymous.Post<LoginResponse>("/api/Auth/login", new { email, password })).Ok();
        var me = (await new ApiClient(NewHttpClient(), token: login.Token).Get<UserResponseDto>("/api/Auth/Me")).Ok();
        return new ApiClient(NewHttpClient(), me.Id, login.Token);
    }

    private async Task<int> CreateBranchAsync(string name) =>
        (await Admin.PostForm<BranchResponseDto>("/api/Branches/Create", Form(("Name", name), ("Description", "بيانات اختبار")))).Ok().Id;

    private async Task<int> CreateDepartmentAsync(string name, int branchId) =>
        (await Admin.PostForm<DepartmentResponseDto>("/api/Department/Create", Form(("Name", name), ("Description", "بيانات اختبار"), ("BranchId", branchId.ToString())))).Ok().Id;

    private async Task<int> CreateRoleAsync(string name, IEnumerable<string> names, IReadOnlyList<PermissionResponseDto> all)
    {
        var fields = new List<KeyValuePair<string, string>> { new("Name", name) };
        foreach (var permission in names)
            fields.Add(new("PermissionIds", all.Single(p => p.Name == permission).Id.ToString()));
        return (await Admin.PostForm<RoleResponseDto>("/api/Roles/Create", fields)).Ok().Id;
    }

    private async Task CreateUserAsync(string fullName, string email, int roleId, int branchId, int departmentId) =>
        (await Admin.PostForm<UserResponseDto>("/api/Users/Create", Form(
            ("FullName", fullName), ("Email", email), ("Password", Password), ("RoleId", roleId.ToString()),
            ("BranchId", branchId.ToString()), ("DepartmentId", departmentId.ToString())))).Ok();

    private static List<KeyValuePair<string, string>> Form(params (string Key, string Value)[] fields) =>
        fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)).ToList();

    public async Task DisposeAsync()
    {
        if (_factory != null) await _factory.DisposeAsync();
        await _sql.DisposeAsync();
    }
}

/// <summary>كل صفوف الاختبار تتشارك البيئة نفسها (حاوية واحدة)، وتعمل بالتتابع</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}
