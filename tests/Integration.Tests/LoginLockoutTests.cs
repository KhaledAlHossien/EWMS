using System.Net;
using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Enums;
using Infrastructure.Persistence.Data;
using Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests;

/// <summary>
/// حماية الدخول (قرار 2026-10-13): خمس محاولات خاطئة متتالية تقفل الحساب 15 دقيقة، والكلمة الصحيحة أثناء القفل مرفوضة،
/// والبريد المجهول يشبه الكلمة الخاطئة، وتعيين المسؤول كلمة جديدة يفتح الحساب. تُستدعى خدمة الدخول مباشرة لأن حدّ الطلبات
/// (20 في الدقيقة لكل جهاز) سيحجب سلسلة المحاولات عبر HTTP.
/// </summary>
[Collection(ApiCollection.Name)]
public class LoginLockoutTests(ApiFixture api)
{
    private async Task<(string Email, int UserId)> NewUserAsync()
    {
        var role = await api.CreateRoleAsync("قفل " + Guid.NewGuid().ToString("N")[..8], ["ViewNotifications"], api.AllPermissions);
        var email = $"lock-{Guid.NewGuid():N}"[..20] + "@test.sy";
        var id = await api.CreateUserAsync("حساب القفل", email, role, api.BranchId, api.DepartmentId);
        return (email, id);
    }

    private async Task<LoginOutcome> LoginAsync(string email, string password)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(new LoginRequest { Email = email, Password = password });
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_and_even_the_right_one_is_refused_meanwhile()
    {
        var (email, userId) = await NewUserAsync();

        for (var i = 1; i < LoginRules.MaxFailedAttempts; i++)
        {
            var wrong = await LoginAsync(email, "wrong-" + i);
            Assert.Null(wrong.Response);
            Assert.Null(wrong.LockedUntil);          // قبل الخامسة: «بيانات غير صحيحة» فقط، بلا إشارة قفل
        }

        var fifth = await LoginAsync(email, "wrong-5");
        Assert.NotNull(fifth.LockedUntil);
        var minutes = (fifth.LockedUntil!.Value - DateTime.UtcNow).TotalMinutes;
        Assert.InRange(minutes, LoginRules.LockoutMinutes - 1, LoginRules.LockoutMinutes);

        var rightButLocked = await LoginAsync(email, ApiFixture.Password);
        Assert.Null(rightButLocked.Response);
        Assert.NotNull(rightButLocked.LockedUntil);

        // صاحب الحساب يُشعَر بالقفل
        await using var scope = api.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<DataContext>().Notifications
            .AnyAsync(n => n.UserId == userId && n.Type == NotificationType.LoginLocked));
    }

    [Fact]
    public async Task A_correct_login_resets_the_failure_counter()
    {
        var (email, _) = await NewUserAsync();

        for (var i = 0; i < LoginRules.MaxFailedAttempts - 1; i++) await LoginAsync(email, "wrong-" + i);
        Assert.NotNull((await LoginAsync(email, ApiFixture.Password)).Response);

        // العدّاد صُفّر: أربع خاطئة جديدة لا تقفل، والخامسة من جديد هي التي تقفل
        for (var i = 0; i < LoginRules.MaxFailedAttempts - 1; i++) Assert.Null((await LoginAsync(email, "again-" + i)).LockedUntil);
        Assert.NotNull((await LoginAsync(email, "last")).LockedUntil);
    }

    [Fact]
    public async Task An_unknown_email_and_a_wrong_password_look_the_same()
    {
        var (email, _) = await NewUserAsync();

        var unknown = await LoginAsync($"nobody-{Guid.NewGuid():N}@test.sy", "whatever1");
        var wrong = await LoginAsync(email, "whatever1");

        Assert.Equal(LoginOutcome.Invalid, unknown);
        Assert.Equal(LoginOutcome.Invalid, wrong);
    }

    [Fact]
    public async Task An_admin_setting_a_new_password_unlocks_the_account_and_ends_old_sessions()
    {
        var (email, userId) = await NewUserAsync();
        var oldSession = await api.LoginAsync(email, ApiFixture.Password);

        for (var i = 0; i < LoginRules.MaxFailedAttempts; i++) await LoginAsync(email, "bad-" + i);
        Assert.NotNull((await LoginAsync(email, ApiFixture.Password)).LockedUntil);

        var user = (await api.Admin.Get<UserResponseDto>($"/api/Users/Get/{userId}")).Ok();
        Assert.NotNull(user.LockedUntil);

        (await api.Admin.PutForm<UserResponseDto>($"/api/Users/Update/{userId}", [
            new("FullName", user.FullName), new("Email", email), new("Password", "New-Pass-9876"), new("RoleId", user.RoleId.ToString()),
            new("BranchId", api.BranchId.ToString()), new("DepartmentId", api.DepartmentId.ToString()), new("IsActive", "true")])).Ok();

        Assert.NotNull((await LoginAsync(email, "New-Pass-9876")).Response);

        // الجلسة القديمة أُنهيت بتغيير الكلمة
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldSession.Get<UserResponseDto>("/api/Auth/Me")).Status);
    }
}
