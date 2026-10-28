using System.Net;
using Application.DTOs.Response;
using Infrastructure.Persistence.Data;
using Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests;

/// <summary>
/// كلمات سر الأجهزة (قرار 2026-10-05): مشفّرة في القاعدة، لا تظهر في أي قائمة ولا استجابة، وتُكشف بصلاحية خاصة
/// ويُسجَّل كل كشف. تُختبر على التطبيق والقاعدة الحقيقيين لأن التسرّب قد يحدث في أي طبقة بينهما.
/// </summary>
[Collection(ApiCollection.Name)]
public class DevicePasswordTests(ApiFixture api)
{
    private const string Secret = "S3cret-كلمة-سر!";

    private async Task<(int SiteId, int DeviceId)> NewSiteAndDeviceAsync()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var site = (await api.Admin.Post<SiteResponseDto>("/api/Sites/Create", new
        {
            name = "موقع " + id, description = "اختبار", latitude = 33.5138, longitude = 36.2765,
            contactName = "", contactPhone = "", responsibleParty = ""
        })).Ok();
        var device = (await api.Admin.Post<DeviceResponseDto>("/api/Devices/Create", new
        {
            name = "جهاز " + id, model = "M-" + id, description = "", category = "كاميرا", manufacturer = ""
        })).Ok();
        return (site.Id, device.Id);
    }

    private Task<ApiResult<DeviceSiteResponseDto>> CreateInstallationAsync(ApiClient client, int siteId, int deviceId, string pass) =>
        client.Post<DeviceSiteResponseDto>("/api/DeviceSites/Create", new
        {
            deviceId, siteId, departmentId = api.DepartmentId, ip = "10.10.10." + Random.Shared.Next(2, 250), subnetMask = "255.255.255.0", userName = "admin", pass,
            status = 1
        });

    [Fact]
    public async Task The_password_is_encrypted_in_the_database_and_never_returned_by_the_api()
    {
        var (siteId, deviceId) = await NewSiteAndDeviceAsync();
        var created = (await CreateInstallationAsync(api.Admin, siteId, deviceId, Secret)).Ok();
        Assert.True(created.HasPassword);

        // القاعدة: نص مشفّر بصيغة enc:v1 لا يحوي كلمة السر
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<DataContext>().DeviceSites.AsNoTracking()
                .Where(d => d.Id == created.Id).Select(d => d.Pass).SingleAsync();
            Assert.StartsWith("enc:v1:", stored);
            Assert.DoesNotContain("S3cret", stored);
        }

        // الاستجابات: لا قائمة ولا تفصيل ولا بحث يحمل كلمة السر
        var one = await api.Admin.Get<DeviceSiteResponseDto>($"/api/DeviceSites/Get/{created.Id}");
        var search = await api.Admin.Get<PagedResultDto<DeviceSiteResponseDto>>($"/api/DeviceSites/Search?siteId={siteId}");
        Assert.DoesNotContain("S3cret", one.Raw);
        Assert.DoesNotContain("S3cret", search.Raw);
        Assert.DoesNotContain("S3cret", (await api.Admin.Get<DeviceSiteResponseDto>($"/api/DeviceSites/Get/{created.Id}")).Raw);
    }

    [Fact]
    public async Task Revealing_needs_its_own_permission_returns_the_clear_text_and_is_logged_without_it()
    {
        var (siteId, deviceId) = await NewSiteAndDeviceAsync();
        var id = (await CreateInstallationAsync(api.Admin, siteId, deviceId, Secret)).Ok().Id;

        var revealed = await api.Admin.Get<DevicePasswordDto>($"/api/DeviceSites/Password/{id}");
        Assert.Equal(Secret, revealed.Ok().Password);

        await using var scope = api.Services.CreateAsyncScope();
        var details = await scope.ServiceProvider.GetRequiredService<DataContext>().DeviceInventoryLogs.AsNoTracking()
            .Where(l => l.EntityType == Domain.Entities.DeviceInventoryEntity.Installation && l.EntityId == id).Select(l => l.Details + "|" + l.Title).ToListAsync();
        Assert.DoesNotContain(details, d => d.Contains("S3cret"));
        Assert.True(details.Count >= 2, "الإنشاء والكشف كلاهما مسجَّل");
    }

    [Fact]
    public async Task A_user_who_can_manage_installations_but_lacks_the_reveal_permission_is_refused()
    {
        var (siteId, deviceId) = await NewSiteAndDeviceAsync();
        var role = await api.CreateRoleAsync("فني أجهزة " + Guid.NewGuid().ToString("N")[..8], ["CreateDevice", "ViewDevices", "ViewSites"], api.AllPermissions);
        var email = $"dev-{Guid.NewGuid():N}"[..20] + "@test.sy";
        await api.CreateUserAsync("فني أجهزة", email, role, api.BranchId, api.DepartmentId);
        var technician = await api.LoginAsync(email, ApiFixture.Password);

        // الفني يضيف تركيباً في قسمه (داخل حدّه) لكنه لا يكشف كلمته
        var mine = (await CreateInstallationAsync(technician, siteId, deviceId, Secret)).Ok();
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.Get<DevicePasswordDto>($"/api/DeviceSites/Password/{mine.Id}")).Status);
        Assert.DoesNotContain("S3cret", (await technician.Get<DeviceSiteResponseDto>($"/api/DeviceSites/Get/{mine.Id}")).Raw);
    }

    [Fact]
    public async Task Updating_with_an_empty_password_keeps_the_current_one()
    {
        var (siteId, deviceId) = await NewSiteAndDeviceAsync();
        var created = (await CreateInstallationAsync(api.Admin, siteId, deviceId, Secret)).Ok();

        (await api.Admin.Put<DeviceSiteResponseDto>($"/api/DeviceSites/Update/{created.Id}", new
        {
            deviceId, siteId, departmentId = api.DepartmentId, ip = created.Ip, subnetMask = "255.255.255.0", userName = "admin2", pass = "", status = 1, rowVersion = created.RowVersion
        })).Ok();

        Assert.Equal(Secret, (await api.Admin.Get<DevicePasswordDto>($"/api/DeviceSites/Password/{created.Id}")).Ok().Password);
    }
}
