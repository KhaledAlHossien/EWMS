using Application.DTOs.Request;
using Application.Features.CentralStudies.Responses;
using Domain.Entities.CentralStudiesBranch;
using Xunit;

namespace StudyBeneficiaries.Tests;

public class StudyResponsePricingTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("150.25", true)]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("0.0000001", false)]
    public void Accepts_an_optional_positive_rate_within_the_storage_precision(string? rate, bool valid)
    {
        var form = new StudyResponseFormDto
        {
            ResponseDate = new DateTime(2026, 11, 18), Recommendation = 1,
            UsdToSypRate = rate == null ? null : decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture)
        };
        Assert.Equal(valid, new StudyResponseFormValidator().Validate(form).IsValid);
    }

    [Fact]
    public void Stores_the_rate_separately_from_the_prices_and_never_reverts_to_legacy_currency()
    {
        var response = new StudyAssignmentResponse { InitialCost = 1500m };
        StudyResponseSupport.Apply(new StudyResponseFormDto { InitialCost = 1500m }, response);
        Assert.False(response.CostsInUsd);
        Assert.Null(response.UsdToSypRate);
        Assert.Throws<InvalidOperationException>(() => StudyResponseSupport.Apply(new StudyResponseFormDto { UsdToSypRate = 150m }, response));

        StudyResponseSupport.Apply(new StudyResponseFormDto
        {
            CostsInUsd = true, InitialCost = 10m, UsdToSypRate = 150m,
            CostItems = [new() { Description = "بند", Unit = "م²", Quantity = 2, UnitPrice = 12.5m }]
        }, response);
        Assert.True(response.CostsInUsd);
        Assert.Equal(150m, response.UsdToSypRate);
        Assert.Equal(10m, response.InitialCost);
        Assert.Equal(12.5m, Assert.Single(response.CostItems).UnitPrice);

        StudyResponseSupport.Apply(new StudyResponseFormDto { InitialCost = 10m }, response);
        Assert.True(response.CostsInUsd);
    }
}
