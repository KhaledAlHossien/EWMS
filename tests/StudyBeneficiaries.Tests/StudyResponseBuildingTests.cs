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
    public void Requires_details_across_building_types_but_allows_an_unchanged_legacy_summary()
    {
        var first = new ResponseBuildingFormDto { BuildingType = 1, BuildingCount = 1, BuildingsArea = 15m };
        var second = new ResponseBuildingFormDto { BuildingType = 2, BuildingCount = 1, BuildingsArea = 25m };
        var form = Form(first, second);
        Assert.Throws<InvalidOperationException>(() => StudyResponseSupport.Apply(form, new()));
        var legacy = new StudyAssignmentResponse
        {
            Buildings = [
                new() { BuildingType = StudyBuildingType.Rehabilitation, BuildingCount = 1, BuildingsArea = 15m },
                new() { BuildingType = StudyBuildingType.NewConstruction, BuildingCount = 1, BuildingsArea = 25m }
            ]
        };
        StudyResponseSupport.Apply(form, legacy);
        Assert.All(legacy.Buildings, b => Assert.Empty(b.Details));
        first.BuildingCount = 2;
        Assert.Throws<InvalidOperationException>(() => StudyResponseSupport.Apply(form, legacy));
        Assert.Equal(1, legacy.Buildings[0].BuildingCount);
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

    private static StudyResponseFormDto Form(params ResponseBuildingFormDto[] buildings) => new()
    {
        ResponseDate = new DateTime(2026, 11, 18), Recommendation = 1, Buildings = buildings.ToList()
    };
}
