using System.Net;
using Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Application.DTOs.Response;
using Integration.Tests.Infrastructure;

namespace Integration.Tests;

/// <summary>
/// رحلة الإجازة كاملة على التطبيق الحقيقي: الدفع يُحدَّد عند الاعتماد النهائي فقط، والحد الشهري (يومان) مشترك ويُحسب من المعتمد،
/// والاعتماد المتزامن لا يصرف الحد مرتين، والمرحلتان لشخصين مختلفين. أخطاء هنا تغيّر راتباً.
/// كل اختبار في شهر خاص به من 2028 (الدالة Day)، فلا يتعارض مع غيره ولا يتقاسم حصته الشهرية.
/// </summary>
[Collection(ApiCollection.Name)]
public class VacationPaymentTests(ApiFixture api)
{
    private sealed record Staff(ApiClient Employee, ApiClient First, ApiClient Final, VacationTypeResponseDto Paid, VacationTypeResponseDto Unpaid);

    // الدخول محدود بـ20 محاولة في الدقيقة لكل جهاز، فنُنشئ الموظف والمعتمدَين مرة واحدة ونعزل كل اختبار بشهر خاص به
    private static readonly object Gate = new();
    private static Task<Staff>? _shared;

    private Task<Staff> NewStaffAsync()
    {
        lock (Gate) return _shared ??= CreateStaffAsync();
    }

    private async Task<Staff> CreateStaffAsync()
    {
        var id = Guid.NewGuid().ToString("N")[..10];
        var all = api.AllPermissions;
        var employeeRole = await api.CreateRoleAsync("موظف " + id, ["CreateVacation", "ViewVacations", "ViewVacationTypes", "CancelVacation"], all);
        var firstRole = await api.CreateRoleAsync("أولى " + id, ["ApproveVacationFirst", "ViewVacations"], all);
        var finalRole = await api.CreateRoleAsync("نهائي " + id, ["ApproveVacationFinal", "ViewVacations"], all);

        await api.CreateUserAsync("موظف " + id, $"emp-{id}@test.sy", employeeRole, api.BranchId, api.DepartmentId);
        await api.CreateUserAsync("أولى " + id, $"first-{id}@test.sy", firstRole, api.BranchId, api.DepartmentId);
        await api.CreateUserAsync("نهائي " + id, $"final-{id}@test.sy", finalRole, api.BranchId, api.DepartmentId);

        var employee = await api.LoginAsync($"emp-{id}@test.sy", ApiFixture.Password);
        var types = (await employee.Get<List<VacationTypeResponseDto>>("/api/VacationTypes/GetAll")).Ok();
        return new Staff(employee, await api.LoginAsync($"first-{id}@test.sy", ApiFixture.Password),
            await api.LoginAsync($"final-{id}@test.sy", ApiFixture.Password), types.First(t => t.IsPaid), types.First(t => !t.IsPaid));
    }

    /// <summary>أول اثنين في الشهر (month) من سنة 2028، ثم أيام نسبية: +0 اثنين، +4 جمعة، +5 سبت، +7 اثنين التالي. كل اختبار شهر مستقل.</summary>
    private static string Day(int month, int offset)
    {
        var first = new DateTime(2028, month, 1);
        var monday = first.AddDays((8 - (int)first.DayOfWeek) % 7);
        return monday.AddDays(offset).ToString("yyyy-MM-dd");
    }

    private static Task<ApiResult<List<VacationResponseDto>>> SubmitAsync(ApiClient employee, int typeId, string start, string end) =>
        employee.PostForm<List<VacationResponseDto>>("/api/Vacations/Create", [
            new("VacationTypeId", typeId.ToString()), new("VacReason", "اختبار"), new("StartVac", start), new("EndVac", end)]);

    private static async Task<int> SubmitOkAsync(ApiClient employee, int typeId, string start, string end) =>
        (await SubmitAsync(employee, typeId, start, end)).Ok().Single().Id;

    private static Task<ApiResult<System.Text.Json.JsonElement>> DecideAsync(ApiClient approver, int id, bool approve, string? reason = null) =>
        approver.Put<System.Text.Json.JsonElement>($"/api/Vacations/Approve/{id}", new { approve, reason });

    private static async Task<VacationResponseDto> GetAsync(ApiClient viewer, int id) =>
        (await viewer.Get<VacationResponseDto>($"/api/Vacations/Get/{id}")).Ok();

    /// <summary>يمرّ بالمرحلتين: الأولى ثم النهائية، ويعيد حالة الطلب بعد الاعتماد</summary>
    private static async Task<VacationResponseDto> ApproveFullyAsync(Staff s, int id)
    {
        (await DecideAsync(s.First, id, true)).Ok();
        (await DecideAsync(s.Final, id, true)).Ok();
        return await GetAsync(s.Employee, id);
    }

    // ───────── الأيام والدفع ─────────

    [Fact]
    public async Task A_pending_request_reserves_no_paid_days_and_payment_is_decided_at_final_approval()
    {
        var s = await NewStaffAsync();
        var a = await SubmitOkAsync(s.Employee, s.Paid.Id, Day(1, 0), Day(1, 1));   // اثنان وثلاثاء
        var b = await SubmitOkAsync(s.Employee, s.Paid.Id, Day(1, 2), Day(1, 3));   // أربعاء وخميس

        var pending = await GetAsync(s.Employee, a);
        Assert.False(pending.PaymentDecided);
        Assert.Equal(0, pending.PaidDays);
        Assert.Contains("عند الاعتماد", pending.PaymentStatusAr);

        // اعتماد الأحدث أولاً يأخذ الحصة المدفوعة كاملة: التقديم لم يحجز شيئاً
        var approvedB = await ApproveFullyAsync(s, b);
        Assert.Equal((2, 0), (approvedB.PaidDays, approvedB.UnpaidDays));

        var approvedA = await ApproveFullyAsync(s, a);
        Assert.Equal((0, 2), (approvedA.PaidDays, approvedA.UnpaidDays));
        Assert.False(approvedA.IsPaid);
    }

    [Fact]
    public async Task The_monthly_paid_limit_is_pooled_across_paid_types_and_a_non_paid_type_uses_none()
    {
        var s = await NewStaffAsync();
        var types = (await s.Employee.Get<List<VacationTypeResponseDto>>("/api/VacationTypes/GetAll")).Ok().Where(t => t.IsPaid).Take(2).ToList();
        Assert.Equal(2, types.Count);

        var unpaid = await SubmitOkAsync(s.Employee, s.Unpaid.Id, Day(2, 0), Day(2, 1));
        var firstPaid = await SubmitOkAsync(s.Employee, types[0].Id, Day(2, 2), Day(2, 2));
        var secondPaid = await SubmitOkAsync(s.Employee, types[1].Id, Day(2, 3), Day(2, 7));   // خميس + سبت + أحد + اثنين (الجمعة لا تُحسب)

        var u = await ApproveFullyAsync(s, unpaid);
        Assert.Equal((0, 2), (u.PaidDays, u.UnpaidDays));

        var one = await ApproveFullyAsync(s, firstPaid);
        Assert.Equal((1, 0), (one.PaidDays, one.UnpaidDays));

        // اليوم المدفوع الثاني فقط يبقى من الحصة (مشتركة بين النوعين)، والبقية غير مدفوعة؛ ومارس 8 ضمن مارس أيضاً
        var two = await ApproveFullyAsync(s, secondPaid);
        Assert.Equal(4, two.VacDayCount);
        Assert.Equal((1, 3), (two.PaidDays, two.UnpaidDays));
    }

    [Fact]
    public async Task Fridays_do_not_count_and_a_request_made_only_of_a_friday_is_refused()
    {
        var s = await NewStaffAsync();

        var request = (await SubmitAsync(s.Employee, s.Paid.Id, Day(3, 3), Day(3, 7))).Ok().Single();
        Assert.Equal(4, request.VacDayCount);       // خميس وسبت وأحد واثنين
        Assert.Equal(5, request.CalendarDays);

        var onlyFriday = await SubmitAsync(s.Employee, s.Paid.Id, Day(3, 11), Day(3, 11));   // جمعة
        Assert.Equal(HttpStatusCode.BadRequest, onlyFriday.Status);
    }

    [Fact]
    public async Task Overlapping_requests_and_retroactive_requests_are_refused()
    {
        var s = await NewStaffAsync();
        await SubmitOkAsync(s.Employee, s.Paid.Id, Day(4, 0), Day(4, 2));

        Assert.Equal(HttpStatusCode.BadRequest, (await SubmitAsync(s.Employee, s.Paid.Id, Day(4, 2), Day(4, 4))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await SubmitAsync(s.Employee, s.Paid.Id, "2020-01-01", "2020-01-02")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await SubmitAsync(s.Employee, s.Paid.Id, Day(4, 5), Day(4, 3))).Status);
    }

    [Fact]
    public async Task A_cancelled_request_frees_its_period_and_counts_for_nothing()
    {
        var s = await NewStaffAsync();
        var id = await SubmitOkAsync(s.Employee, s.Paid.Id, Day(5, 0), Day(5, 1));

        (await s.Employee.Put<System.Text.Json.JsonElement>($"/api/Vacations/Cancel/{id}")).Ok();
        Assert.Equal("Cancelled", (await GetAsync(s.Employee, id)).Status);

        // الفترة نفسها صارت متاحة، والحصة كاملة
        var again = await SubmitOkAsync(s.Employee, s.Paid.Id, Day(5, 0), Day(5, 1));
        var approved = await ApproveFullyAsync(s, again);
        Assert.Equal(2, approved.PaidDays);
    }

    // ───────── فصل المرحلتين ─────────

    [Fact]
    public async Task The_same_person_cannot_give_both_approvals_and_nobody_approves_their_own_request()
    {
        var s = await NewStaffAsync();
        var both = await api.CreateRoleAsync("كلتاهما " + Guid.NewGuid().ToString("N")[..8], ["ApproveVacationFirst", "ApproveVacationFinal", "ViewVacations"], api.AllPermissions);
        var email = $"both-{Guid.NewGuid():N}"[..20] + "@test.sy";
        await api.CreateUserAsync("يعتمد المرحلتين", email, both, api.BranchId, api.DepartmentId);
        var approver = await api.LoginAsync(email, ApiFixture.Password);

        var id = await SubmitOkAsync(s.Employee, s.Paid.Id, Day(6, 0), Day(6, 1));
        (await DecideAsync(approver, id, true)).Ok();

        var second = await DecideAsync(approver, id, true);
        Assert.Equal(HttpStatusCode.Forbidden, second.Status);
        Assert.Equal("PendingBranchManager", (await GetAsync(s.Employee, id)).Status);

        // الموظف نفسه لا يوافق على طلبه (لا يملك الصلاحية أصلاً هنا)
        Assert.Equal(HttpStatusCode.Forbidden, (await DecideAsync(s.Employee, id, true)).Status);
    }

    [Fact]
    public async Task A_rejection_stores_its_reason_and_ends_the_request()
    {
        var s = await NewStaffAsync();
        var id = await SubmitOkAsync(s.Employee, s.Paid.Id, Day(7, 0), Day(7, 1));

        (await DecideAsync(s.First, id, false, "ضغط عمل")).Ok();

        var rejected = await GetAsync(s.Employee, id);
        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal("ضغط عمل", rejected.RejectionReason);
        Assert.Equal(HttpStatusCode.BadRequest, (await DecideAsync(s.Final, id, true)).Status);
    }

    // ───────── التزامن ─────────

    [Fact]
    public async Task Simultaneous_final_approvals_for_one_employee_never_spend_the_monthly_quota_twice()
    {
        var s = await NewStaffAsync();
        // اثنا عشر طلب يوم واحد في شهر واحد، كلها بانتظار الاعتماد النهائي (عدد كبير كي يظهر السباق حتى مع تسخين الشيفرة)
        var ids = new List<int>();
        foreach (var offset in new[] { 0, 1, 2, 3, 5, 6, 7, 8, 9, 10, 12, 13 })
        {
            var id = await SubmitOkAsync(s.Employee, s.Paid.Id, Day(8, offset), Day(8, offset));
            (await DecideAsync(s.First, id, true)).Ok();
            ids.Add(id);
        }

        var results = await Task.WhenAll(ids.Select(id => DecideAsync(s.Final, id, true)));
        Assert.All(results, r => r.Ok());

        var approved = await Task.WhenAll(ids.Select(id => GetAsync(s.Employee, id)));
        Assert.All(approved, v => Assert.Equal("Approved", v.Status));
        Assert.Equal(2, approved.Sum(v => v.PaidDays));
        Assert.Equal(approved.Sum(v => v.VacDayCount) - 2, approved.Sum(v => v.UnpaidDays));
    }

    [Fact]
    public async Task The_per_employee_lock_serialises_one_employee_and_lets_different_employees_run_together()
    {
        // اختبار حتمي لأداة القفل نفسها (لا يعتمد على توقيت الطلبات): قراءة ثم نوم ثم كتابة، تضيع التحديثات إن لم يكن القفل حصرياً
        async Task<int> LostUpdateRunAsync(int[] employees)
        {
            var counters = new Dictionary<int, int>();
            foreach (var e in employees.Distinct()) counters[e] = 0;
            await Task.WhenAll(employees.Select(async employee =>
            {
                await using var scope = api.Services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IVacationService>().RunExclusiveForUserAsync(employee, async () =>
                {
                    int seen;
                    lock (counters) seen = counters[employee];
                    await Task.Delay(40);
                    lock (counters) counters[employee] = seen + 1;
                    return 0;
                });
            }));
            return counters.Values.Sum();
        }

        Assert.Equal(8, await LostUpdateRunAsync(Enumerable.Repeat(900001, 8).ToArray()));

        // موظفان مختلفان لا ينتظر أحدهما الآخر: لكل منهما أربعة تحديثات سليمة
        var started = System.Diagnostics.Stopwatch.StartNew();
        Assert.Equal(8, await LostUpdateRunAsync([900002, 900002, 900002, 900002, 900003, 900003, 900003, 900003]));
        Assert.True(started.ElapsedMilliseconds < 8 * 40 + 1500, "قفل موظف لا يجوز أن يعطّل موظفاً آخر");
    }

    [Fact]
    public async Task Two_simultaneous_decisions_on_the_same_request_succeed_only_once()
    {
        var s = await NewStaffAsync();
        var id = await SubmitOkAsync(s.Employee, s.Paid.Id, Day(9, 0), Day(9, 1));
        (await DecideAsync(s.First, id, true)).Ok();

        var second = await api.CreateRoleAsync("نهائي ثانٍ " + Guid.NewGuid().ToString("N")[..8], ["ApproveVacationFinal", "ViewVacations"], api.AllPermissions);
        var email = $"f2-{Guid.NewGuid():N}"[..20] + "@test.sy";
        await api.CreateUserAsync("معتمد ثانٍ", email, second, api.BranchId, api.DepartmentId);
        var other = await api.LoginAsync(email, ApiFixture.Password);

        var outcomes = await Task.WhenAll(DecideAsync(s.Final, id, true), DecideAsync(other, id, true));

        Assert.Single(outcomes, o => o.IsSuccess);
        var rejected = Assert.Single(outcomes, o => !o.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.Status);

        var final = await GetAsync(s.Employee, id);
        Assert.Equal("Approved", final.Status);
        Assert.Equal(2, final.PaidDays);
    }
}
