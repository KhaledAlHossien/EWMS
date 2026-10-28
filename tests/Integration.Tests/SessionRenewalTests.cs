using System.Net;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Infrastructure.Persistence.Data;
using Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests;

/// <summary>
/// تجديد الجلسة (قرار 2026-10-28) على التطبيق الحقيقي: توكن جديد بنفس الهوية، والقديم يبقى دقيقتين فقط، وحد أقصى لعمر الجلسة،
/// وحساب معطّل أو توكن مُبطَل لا يتجدد. الدخول عبر الخدمة مباشرة لأن نقطة الدخول محدودة بـ20 طلباً في الدقيقة.
/// </summary>
[Collection(ApiCollection.Name)]
public class SessionRenewalTests(ApiFixture api)
{
    private async Task<(LoginResponse Login, int UserId, string Email)> NewSessionAsync()
    {
        var role = await api.CreateRoleAsync("جلسة " + Guid.NewGuid().ToString("N")[..8], ["ViewNotifications"], api.AllPermissions);
        var email = $"sess-{Guid.NewGuid():N}"[..20] + "@test.sy";
        var userId = await api.CreateUserAsync("صاحب الجلسة", email, role, api.BranchId, api.DepartmentId);

        await using var scope = api.Services.CreateAsyncScope();
        var outcome = await scope.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(new LoginRequest { Email = email, Password = ApiFixture.Password });
        return (outcome.Response!, userId, email);
    }

    private async Task<T> Db<T>(Func<DataContext, Task<T>> action)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<DataContext>());
    }

    [Fact]
    public async Task Renewing_gives_a_new_working_token_and_shortens_the_old_one_to_a_grace_period()
    {
        var (login, userId, _) = await NewSessionAsync();
        var oldClient = api.WithToken(login.Token, userId);

        var renewed = (await oldClient.Post<LoginResponse>("/api/Auth/Renew")).Ok();

        Assert.NotEqual(login.Token, renewed.Token);
        Assert.True(renewed.ExpiresAt > DateTime.UtcNow.AddMinutes(100));
        Assert.Equal(login.Permissions.Order(), renewed.Permissions.Order());
        Assert.Equal(userId, (await api.WithToken(renewed.Token).Get<UserResponseDto>("/api/Auth/Me")).Ok().Id);

        // القديم ما زال يعمل (طلبات في الطريق) لكنه لن يعيش أكثر من دقيقتين
        Assert.True((await oldClient.Get<UserResponseDto>("/api/Auth/Me")).IsSuccess);
        var oldExpiry = await Db(db => db.UserTokens.Where(t => t.Token == login.Token).Select(t => t.ExpiresAt).SingleAsync());
        Assert.True(oldExpiry <= DateTime.UtcNow.AddSeconds(125));

        // الجلسة الأصلية محمولة إلى التوكن الجديد
        var starts = await Db(db => db.UserTokens.Where(t => t.Token == login.Token || t.Token == renewed.Token).Select(t => t.SessionStartedAt).ToListAsync());
        Assert.Single(starts.Distinct());
    }

    [Fact]
    public async Task A_session_past_its_maximum_age_cannot_be_renewed()
    {
        var (login, userId, _) = await NewSessionAsync();
        await Db(async db =>
        {
            var row = await db.UserTokens.SingleAsync(t => t.Token == login.Token);
            row.SessionStartedAt = DateTime.UtcNow.AddHours(-13);
            return await db.SaveChangesAsync();
        });

        var result = await api.WithToken(login.Token, userId).Post<LoginResponse>("/api/Auth/Renew");

        Assert.Equal(HttpStatusCode.Unauthorized, result.Status);
        Assert.Contains("انتهت جلستك", result.Raw);
    }

    [Fact]
    public async Task A_token_issued_by_renewal_is_cut_at_the_end_of_the_session()
    {
        var (login, userId, _) = await NewSessionAsync();
        var sessionEnd = DateTime.UtcNow.AddMinutes(30);
        await Db(async db =>
        {
            var row = await db.UserTokens.SingleAsync(t => t.Token == login.Token);
            row.SessionStartedAt = sessionEnd.AddHours(-12);
            return await db.SaveChangesAsync();
        });

        var renewed = (await api.WithToken(login.Token, userId).Post<LoginResponse>("/api/Auth/Renew")).Ok();

        Assert.True(renewed.ExpiresAt <= sessionEnd.AddSeconds(5), "التوكن الجديد لا يتجاوز نهاية الجلسة القصوى");
    }

    [Fact]
    public async Task A_logged_out_token_and_a_deactivated_account_cannot_renew()
    {
        var (login, userId, _) = await NewSessionAsync();
        var client = api.WithToken(login.Token, userId);
        (await client.Post<System.Text.Json.JsonElement>("/api/Auth/logout")).Ok();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.Post<LoginResponse>("/api/Auth/Renew")).Status);

        var (second, secondId, _) = await NewSessionAsync();
        await Db(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == secondId);
            user.IsActive = false;
            return await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.WithToken(second.Token, secondId).Post<LoginResponse>("/api/Auth/Renew")).Status);
    }

    [Fact]
    public async Task Renewing_without_a_token_is_refused()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().Post<LoginResponse>("/api/Auth/Renew")).Status);
    }
}
