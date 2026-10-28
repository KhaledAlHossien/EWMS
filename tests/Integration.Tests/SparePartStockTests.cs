using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Maintenance;
using Infrastructure.Persistence.Data;
using Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests;

/// <summary>
/// مخزون قطع الغيار على SQL Server حقيقي: الكمية لا تتغير إلا بحركة ذرّية، ولا يُصرف رصيد واحد مرتين، ولا يصير سالباً،
/// والمتوسط المرجّح وسعر الصرف المثبَّت. الحماية قفل UPDLOCK على صف القطعة، ولا يُختبر هذا إلا على قاعدة حقيقية.
/// </summary>
[Collection(ApiCollection.Name)]
public class SparePartStockTests(ApiFixture api)
{
    private static int _serial;

    private async Task<T> InScope<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    private Task<int> NewPartAsync() => InScope(async sp =>
    {
        var part = await sp.GetRequiredService<ISparePartService>().AddAsync(new SparePart
        {
            DepartmentId = api.DepartmentId,
            Name = $"قطعة اختبار {Interlocked.Increment(ref _serial)}-{Guid.NewGuid():N}",
            Unit = "قطعة"
        });
        return part.Id;
    });

    private Task<int> NewRequestAsync() => InScope(async sp =>
    {
        var db = sp.GetRequiredService<DataContext>();
        var suffix = Guid.NewGuid().ToString("N");
        var type = new DeviceType { Name = "نوع " + suffix };
        var company = new DeviceCompany { Name = "شركة " + suffix };
        var damage = new DamageType { Name = "عطل " + suffix };
        db.AddRange(type, company, damage);
        await db.SaveChangesAsync();

        var device = new DeviceMaintenance { SerialNumber = "SN-" + suffix, DeviceTypeId = type.Id, DeviceCompanyId = company.Id };
        db.Add(device);
        await db.SaveChangesAsync();

        var request = new MaintenanceRequest
        {
            UserId = api.Preparer.UserId,
            DepartmentId = api.DepartmentId,
            DeviceMaintenanceId = device.Id,
            DamageTypeId = damage.Id,
            MaintenanceRequestStatusId = (await db.MaintenanceRequestStatuses.OrderBy(s => s.Id).FirstAsync()).Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Add(request);
        await db.SaveChangesAsync();
        return request.Id;
    });

    private Task<SparePart> PartAsync(int id) => InScope(sp => sp.GetRequiredService<DataContext>().SpareParts.AsNoTracking().SingleAsync(p => p.Id == id));

    private Task<int> MovementCountAsync(int partId) =>
        InScope(sp => sp.GetRequiredService<DataContext>().SparePartMovements.CountAsync(m => m.SparePartId == partId));

    private Task<StockChange> ReceiveAsync(int partId, decimal quantity, decimal cost) =>
        InScope(sp => sp.GetRequiredService<ISparePartService>().ReceiveAsync(partId, quantity, cost, DateTime.Today, "اختبار", api.Admin.UserId));

    // ───────── المتوسط المرجّح ─────────

    [Fact]
    public async Task The_first_receipt_sets_the_cost_and_later_receipts_average_it_by_quantity()
    {
        var part = await NewPartAsync();

        var first = await ReceiveAsync(part, 10, 100);
        Assert.Equal((0m, 10m, 100m), (first.Before, first.After, first.AverageCost));

        var second = await ReceiveAsync(part, 10, 200);
        Assert.Equal((10m, 20m, 150m), (second.Before, second.After, second.AverageCost));

        var third = await ReceiveAsync(part, 20, 100);
        Assert.Equal(125m, third.AverageCost);   // (20×150 + 20×100) ÷ 40

        var stored = await PartAsync(part);
        Assert.Equal(40m, stored.Quantity);
        Assert.Equal(125m, stored.AverageCost);
        Assert.Equal(3, await MovementCountAsync(part));
    }

    // ───────── الصرف والإعادة ─────────

    [Fact]
    public async Task An_issue_freezes_the_cost_at_that_moment_and_a_return_restores_the_stock_at_the_frozen_cost()
    {
        var part = await NewPartAsync();
        var request = await NewRequestAsync();
        await ReceiveAsync(part, 10, 100);

        var (issued, afterIssue) = await InScope(sp => sp.GetRequiredService<ISparePartService>().IssueAsync(request, part, 4, api.Preparer.UserId));
        Assert.Equal(100m, issued.UnitCost);
        Assert.Equal(6m, afterIssue.After);

        // سعر الإدخال يتغير بعد الصرف؛ سعر القطعة المصروفة لا يتغير
        await ReceiveAsync(part, 6, 400);
        Assert.Equal(250m, (await PartAsync(part)).AverageCost);   // (6×100 + 6×400) ÷ 12
        Assert.Equal(100m, (await InScope(sp => sp.GetRequiredService<ISparePartService>().GetRequestPartAsync(issued.Id)))!.UnitCost);

        var requestPart = (await InScope(sp => sp.GetRequiredService<ISparePartService>().GetRequestPartAsync(issued.Id)))!;
        var back = await InScope(sp => sp.GetRequiredService<ISparePartService>().ReturnAsync(requestPart, api.Preparer.UserId));

        Assert.Equal(16m, back.After);
        Assert.Equal(16m, (await PartAsync(part)).Quantity);
        Assert.Null(await InScope(sp => sp.GetRequiredService<ISparePartService>().GetRequestPartAsync(issued.Id)));
        // إدخال + صرف + إدخال + إعادة، والحركات لا تُحذف
        Assert.Equal(4, await MovementCountAsync(part));
    }

    [Fact]
    public async Task Issuing_more_than_the_balance_is_refused_and_leaves_no_trace()
    {
        var part = await NewPartAsync();
        var request = await NewRequestAsync();
        await ReceiveAsync(part, 3, 50);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InScope(sp => sp.GetRequiredService<ISparePartService>().IssueAsync(request, part, 4, api.Preparer.UserId)));

        Assert.Contains("لا تكفي", error.Message);
        Assert.Equal(3m, (await PartAsync(part)).Quantity);
        Assert.Equal(1, await MovementCountAsync(part));
        Assert.Empty(await InScope(sp => sp.GetRequiredService<ISparePartService>().GetRequestPartsAsync(request)));
    }

    [Fact]
    public async Task A_negative_adjustment_beyond_the_balance_is_refused()
    {
        var part = await NewPartAsync();
        await ReceiveAsync(part, 2, 10);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InScope(sp => sp.GetRequiredService<ISparePartService>().AdjustAsync(part, -3, "تالف", api.Admin.UserId)));

        Assert.Equal(2m, (await PartAsync(part)).Quantity);
    }

    [Fact]
    public async Task An_unknown_part_is_a_not_found_error()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => ReceiveAsync(int.MaxValue, 1, 1));
    }

    // ───────── التزامن ─────────

    [Fact]
    public async Task Simultaneous_issues_never_spend_the_same_stock_twice()
    {
        var part = await NewPartAsync();
        await ReceiveAsync(part, 5, 20);
        var requests = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => NewRequestAsync()));

        // 12 طلباً متزامناً لكل منها قطعة واحدة والرصيد 5: ينجح خمسة بالضبط
        var outcomes = await Task.WhenAll(requests.Select(async request =>
        {
            try
            {
                await InScope(sp => sp.GetRequiredService<ISparePartService>().IssueAsync(request, part, 1, api.Preparer.UserId));
                return true;
            }
            catch (InvalidOperationException) { return false; }
        }));

        Assert.Equal(5, outcomes.Count(o => o));
        var stored = await PartAsync(part);
        Assert.Equal(0m, stored.Quantity);
        Assert.Equal(1 + 5, await MovementCountAsync(part));   // الإدخال + خمسة صرف فقط

        // رصيد كل حركة بعدها متسلسل بلا فجوات ولا تكرار
        var balances = await InScope(sp => sp.GetRequiredService<DataContext>().SparePartMovements
            .Where(m => m.SparePartId == part).OrderBy(m => m.Id).Select(m => m.BalanceAfter).ToListAsync());
        Assert.Equal(new decimal[] { 5, 4, 3, 2, 1, 0 }, balances.OrderByDescending(b => b));
    }

    [Fact]
    public async Task Simultaneous_receipts_and_adjustments_keep_the_balance_equal_to_the_sum_of_movements()
    {
        var part = await NewPartAsync();
        await ReceiveAsync(part, 10, 10);

        var jobs = Enumerable.Range(0, 10).Select(i => i % 2 == 0
            ? ReceiveAsync(part, 2, 10)
            : InScope(sp => sp.GetRequiredService<ISparePartService>().AdjustAsync(part, -1, "عدّ", api.Admin.UserId)));
        await Task.WhenAll(jobs);

        var stored = await PartAsync(part);
        var movements = await InScope(sp => sp.GetRequiredService<DataContext>().SparePartMovements.Where(m => m.SparePartId == part).ToListAsync());
        Assert.Equal(10 + 5 * 2 - 5, stored.Quantity);
        Assert.Equal(stored.Quantity, movements.Sum(m => m.Quantity));
        Assert.Equal(stored.Quantity, movements.OrderByDescending(m => m.Id).First().BalanceAfter);
    }

    [Fact]
    public async Task The_database_itself_refuses_a_negative_quantity()
    {
        var part = await NewPartAsync();

        // قيد CHECK في القاعدة هو خط الدفاع الأخير إن أخطأ أي كود لاحق وتجاوز الخدمة
        var error = await Assert.ThrowsAnyAsync<Exception>(() => InScope(sp =>
            sp.GetRequiredService<DataContext>().Database.ExecuteSqlInterpolatedAsync($"UPDATE SpareParts SET Quantity = -1 WHERE Id = {part}")));

        Assert.Contains("CHECK", error.GetBaseException().Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0m, (await PartAsync(part)).Quantity);
    }
}
