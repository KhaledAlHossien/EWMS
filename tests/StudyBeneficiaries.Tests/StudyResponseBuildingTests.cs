using Application.DTOs.Request;
using Application.Features.CentralStudies.Responses;
using Domain.Entities.CentralStudiesBranch;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace StudyBeneficiaries.Tests;

public class StudyResponseBuildingTests
{
    [Fact]
    public void Calculates_the_summary_from_saved_detail_areas_and_keeps_their_order()
    {
        var response = new StudyAssignmentResponse();
        var building = new ResponseBuildingFormDto
        {
            BuildingType = 1, BuildingCount = 2, BuildingsArea = 10,
            Details = [
                new() { Code = "  A-01 ", Function = " إدارة ", Area = 123.45m },
                new() { Code = "A-02", Function = "مختبر", Area = 0.55m }
            ]
        };
        var form = Form(building);
        Assert.True(new StudyResponseFormValidator().Validate(form).IsValid);
        StudyResponseSupport.Apply(form, response);
        var saved = Assert.Single(response.Buildings);
        Assert.Equal(124m, saved.BuildingsArea);
        Assert.Collection(saved.Details,
            first => Assert.Equal(("A-01", "إدارة", 123.45m, 1), (first.Code, first.Function, first.Area, first.SortOrder)),
            second => Assert.Equal(("A-02", "مختبر", 0.55m, 2), (second.Code, second.Function, second.Area, second.SortOrder)));
    }

    [Fact]
    public void Rejects_the_legacy_summary_even_when_it_is_unchanged()
    {
        // الشكل القديم (عدد ومساحة بلا تفاصيل) لم يعد مقبولاً ولو كان محفوظاً كذلك
        var legacy = new StudyAssignmentResponse
        {
            Buildings = [new() { BuildingType = StudyBuildingType.Rehabilitation, BuildingCount = 1, BuildingsArea = 15m }]
        };
        var form = Form(new ResponseBuildingFormDto { BuildingType = 1, BuildingCount = 1, BuildingsArea = 15m });
        Assert.False(new StudyResponseFormValidator().Validate(form).IsValid);
        Assert.Throws<InvalidOperationException>(() => StudyResponseSupport.Apply(form, legacy));
        Assert.Empty(legacy.Buildings[0].Details);
    }

    [Fact]
    public void Rejects_a_building_count_that_differs_from_its_detail_rows()
    {
        var group = Group(1, ("A-01", "إدارة", 10m), ("A-02", "مختبر", 15m));
        var form = Form(group);
        group.BuildingCount = 3;
        Assert.False(new StudyResponseFormValidator().Validate(form).IsValid);
        Assert.Throws<InvalidOperationException>(() => StudyResponseSupport.Apply(form, new()));
    }

    [Fact]
    public void Requires_one_cost_item_per_building_in_order()
    {
        var form = Form(Group(2, ("N-01", "مدرسة", 100m)), Group(1, ("R-01", "مستودع", 40m)));
        // الترتيب: إعادة تأهيل ثم إنشاء جديد، مهما كان ترتيب الأنواع في الطلب
        Assert.Equal(["إعادة تأهيل — R-01 — مستودع", "إنشاء جديد — N-01 — مدرسة"], form.CostItems.Select(c => c.Description));
        Assert.True(new StudyResponseFormValidator().Validate(form).IsValid);

        form.CostItems[0].Quantity = 41m;                       // الكمية ≠ مساحة المبنى
        Assert.False(new StudyResponseFormValidator().Validate(form).IsValid);
        Assert.Throws<InvalidOperationException>(() => StudyResponseSupport.Apply(form, new()));
        form.CostItems[0].Quantity = 40m;

        form.CostItems[1].Unit = "مقطوعية";                     // وحدة غير م²
        Assert.False(new StudyResponseFormValidator().Validate(form).IsValid);
        form.CostItems[1].Unit = "م²";

        form.CostItems.RemoveAt(1);                             // بند ناقص
        Assert.False(new StudyResponseFormValidator().Validate(form).IsValid);
        Assert.Throws<InvalidOperationException>(() => StudyResponseSupport.Apply(form, new()));
    }

    [Fact]
    public async Task Editing_a_building_group_replaces_its_saved_detail_rows()
    {
        using var db = new DataContext(new DbContextOptionsBuilder<DataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var response = new StudyAssignmentResponse();
        var group = new ResponseBuildingFormDto
        {
            BuildingType = 1, BuildingCount = 2, Details = [
                new() { Code = "A-01", Function = "إدارة", Area = 10m },
                new() { Code = "A-02", Function = "مختبر", Area = 15m }
            ]
        };
        StudyResponseSupport.Apply(Form(group), response);
        db.StudyAssignmentResponses.Add(response);
        await db.SaveChangesAsync();
        group.Details[0].Area = 20m;
        StudyResponseSupport.Apply(Form(group), response);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var saved = await db.ResponseBuildings.Include(b => b.Details).SingleAsync();
        Assert.Equal(35m, saved.BuildingsArea);
        Assert.Equal(new[] { 20m, 15m }, saved.Details.OrderBy(d => d.SortOrder).Select(d => d.Area));
        Assert.Equal(2, await db.Set<ResponseBuildingDetail>().CountAsync());
    }

    /// <summary>نموذج بالشكل الموحّد: بند تكلفة لكل سطر تفاصيل، بالترتيب نفسه</summary>
    private static StudyResponseFormDto Form(params ResponseBuildingFormDto[] buildings) => new()
    {
        ResponseDate = new DateTime(2026, 11, 18), Recommendation = 1, Buildings = buildings.ToList(),
        CostItems = buildings.OrderBy(b => b.BuildingType).SelectMany(b => b.Details.Select(d => new ResponseCostItemFormDto
        {
            Description = StudyResponseBuildings.Description((StudyBuildingType)b.BuildingType, d.Code, d.Function),
            Unit = StudyResponseBuildings.Unit, Quantity = d.Area, UnitPrice = 10m
        })).ToList()
    };

    private static ResponseBuildingFormDto Group(int type, params (string Code, string Function, decimal Area)[] rows) => new()
    {
        BuildingType = type, BuildingCount = rows.Length,
        Details = rows.Select(r => new ResponseBuildingDetailFormDto { Code = r.Code, Function = r.Function, Area = r.Area }).ToList()
    };
}
