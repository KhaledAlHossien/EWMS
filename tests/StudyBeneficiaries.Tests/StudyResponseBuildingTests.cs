using System.Text.Json;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.CentralStudies;
using Application.Features.CentralStudies.Responses;
using Application.Helper.Profiles;
using AutoMapper;
using Domain.Entities.CentralStudiesBranch;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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

    [Fact]
    public void Repetitions_multiply_the_area_and_the_cost_of_each_building()
    {
        var group = Group(2, ("N-01", "صف دراسي", 100m), ("N-02", "إدارة", 40.5m));
        group.Details[0].Repetitions = 5;
        group.Details[0].Note = "  نموذج مكرر  ";
        var form = Form(group);
        form.SiteArea = 5000m;
        form.SiteNote = "  أرض مستوية  ";
        Assert.Equal([5, 1], form.CostItems.Select(c => c.Repetitions));
        Assert.True(new StudyResponseFormValidator().Validate(form).IsValid);

        var response = new StudyAssignmentResponse();
        StudyResponseSupport.Apply(form, response);
        var saved = Assert.Single(response.Buildings);
        Assert.Equal(540.5m, saved.BuildingsArea);               // 100 × 5 + 40.5 × 1
        Assert.Equal(2, saved.BuildingCount);                     // عدد الأسطر لا يشمل التكرار
        Assert.Equal((5, "نموذج مكرر"), (saved.Details[0].Repetitions, saved.Details[0].Note));
        Assert.Equal((1, (string?)null), (saved.Details[1].Repetitions, saved.Details[1].Note));
        Assert.Equal("أرض مستوية", response.SiteNote);
        Assert.Equal([5, 1], response.CostItems.Select(c => c.Repetitions));

        // الكلفة كاملة والمساحة كاملة في الرد المعروض
        response.Assignment = new StudyAssignment { Request = new StudyRequest() };
        var dto = Mapper.Map<StudyResponseDto>(response);
        var building = Assert.Single(dto.Buildings);
        Assert.Equal((540.5m, 6), (building.BuildingsArea, building.TotalRepetitions));
        Assert.Equal([500m, 40.5m], building.Details.Select(d => d.TotalArea));
        Assert.Equal([5000m, 405m], dto.CostItems.Select(c => c.Total));   // 100 × 10 × 5 ، 40.5 × 10 × 1
        Assert.Equal(5405m, dto.TotalCost);
        Assert.Equal("أرض مستوية", dto.SiteNote);
    }

    [Fact]
    public void Requires_each_cost_item_to_carry_its_building_repetitions()
    {
        var group = Group(1, ("R-01", "مستودع", 40m));
        group.Details[0].Repetitions = 3;
        var form = Form(group);
        form.CostItems[0].Repetitions = 1;                        // التكرار ≠ تكرار المبنى
        Assert.False(new StudyResponseFormValidator().Validate(form).IsValid);
        Assert.Throws<InvalidOperationException>(() => StudyResponseSupport.Apply(form, new()));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-2, false)]
    [InlineData(1, true)]
    [InlineData(StudyResponseFormValidator.MaxRepetitions, true)]
    [InlineData(StudyResponseFormValidator.MaxRepetitions + 1, false)]
    public void Limits_the_repetitions_of_a_building(int repetitions, bool valid)
    {
        var group = Group(1, ("R-01", "مستودع", 40m));
        group.Details[0].Repetitions = repetitions;
        Assert.Equal(valid, new StudyResponseFormValidator().Validate(Form(group)).IsValid);
    }

    [Fact]
    public void Limits_the_building_and_site_notes()
    {
        var group = Group(1, ("R-01", "مستودع", 40m));
        group.Details[0].Note = new string('م', 501);
        Assert.False(new StudyResponseFormValidator().Validate(Form(group)).IsValid);
        group.Details[0].Note = new string('م', 500);
        var form = Form(group);
        Assert.True(new StudyResponseFormValidator().Validate(form).IsValid);
        form.SiteNote = new string('م', 1001);
        Assert.False(new StudyResponseFormValidator().Validate(form).IsValid);
    }

    [Fact]
    public void Accepts_the_three_building_types_once_each_in_type_order()
    {
        var form = Form(Group(3, ("P-01", "مخبر", 30m)), Group(2, ("N-01", "مدرسة", 100m)), Group(1, ("R-01", "مستودع", 40m)));
        Assert.Equal(["إعادة تأهيل — R-01 — مستودع", "إنشاء جديد — N-01 — مدرسة", "إعادة تأهيل جزئي — P-01 — مخبر"],
            form.CostItems.Select(c => c.Description));
        Assert.True(new StudyResponseFormValidator().Validate(form).IsValid);
        Assert.Equal("إعادة تأهيل جزئي", StudyRules.Name(StudyBuildingType.PartialRehabilitation));

        var twice = Form(Group(3, ("P-01", "مخبر", 30m)), Group(3, ("P-02", "مخبر", 30m)));
        Assert.False(new StudyResponseFormValidator().Validate(twice).IsValid);
        var unknown = Form(Group(4, ("X-01", "مخبر", 30m)));
        Assert.False(new StudyResponseFormValidator().Validate(unknown).IsValid);
    }

    [Fact]
    public void A_form_that_does_not_send_repetitions_keeps_one_repetition()
    {
        // الواجهة الحالية لا ترسل الحقل: يبقى التكرار 1 فلا يتغير شيء
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.Equal(1, JsonSerializer.Deserialize<ResponseBuildingDetailFormDto>(
            @"{""code"":""A"",""function"":""B"",""area"":5}", web)!.Repetitions);
        Assert.Equal(1, JsonSerializer.Deserialize<ResponseCostItemFormDto>(
            @"{""description"":""x"",""unit"":""م²"",""quantity"":5,""unitPrice"":2}", web)!.Repetitions);
    }

    private static readonly IMapper Mapper =
        new MapperConfiguration(c => c.AddProfile<CentralStudiesProfile>(), NullLoggerFactory.Instance).CreateMapper();

    /// <summary>نموذج بالشكل الموحّد: بند تكلفة لكل سطر تفاصيل، بالترتيب نفسه</summary>
    private static StudyResponseFormDto Form(params ResponseBuildingFormDto[] buildings) => new()
    {
        ResponseDate = new DateTime(2026, 11, 18), Recommendation = 1, Buildings = buildings.ToList(),
        CostItems = buildings.OrderBy(b => b.BuildingType).SelectMany(b => b.Details.Select(d => new ResponseCostItemFormDto
        {
            Description = StudyResponseBuildings.Description((StudyBuildingType)b.BuildingType, d.Code, d.Function),
            Unit = StudyResponseBuildings.Unit, Quantity = d.Area, UnitPrice = 10m, Repetitions = d.Repetitions
        })).ToList()
    };

    private static ResponseBuildingFormDto Group(int type, params (string Code, string Function, decimal Area)[] rows) => new()
    {
        BuildingType = type, BuildingCount = rows.Length,
        Details = rows.Select(r => new ResponseBuildingDetailFormDto { Code = r.Code, Function = r.Function, Area = r.Area }).ToList()
    };
}
