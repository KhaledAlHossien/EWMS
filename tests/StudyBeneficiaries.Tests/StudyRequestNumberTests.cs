using Application.DTOs.Request;
using Application.Features.CentralStudies.Requests;
using Application.Features.CentralStudies.Requests.Commands.Create;
using Application.Features.CentralStudies.Requests.Commands.Update;
using Application.Helper.Profiles;
using AutoMapper;
using Domain.Entities.CentralStudiesBranch;
using Infrastructure.Persistence.Data;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace StudyBeneficiaries.Tests;

public class StudyRequestNumberTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234567890123456789012345678901")]
    public void Create_and_update_require_a_manual_number_within_the_database_limit(string? number)
    {
        var dto = ValidRequest();
        dto.RequestNumber = number!;
        var create = new CreateStudyRequestCommandValidator().Validate(new CreateStudyRequestCommand(dto));
        var update = new UpdateStudyRequestCommandValidator().Validate(new UpdateStudyRequestCommand(1, dto));
        Assert.Contains(create.Errors, e => e.PropertyName == "Dto.RequestNumber");
        Assert.Contains(update.Errors, e => e.PropertyName == "Dto.RequestNumber");
    }

    [Theory]
    [InlineData("٠٠١/د/2026")]
    [InlineData("001-2026")]
    [InlineData("123456789012345678901234567890")]
    public void Manual_numbers_are_valid_without_a_second_diwan_field(string number)
    {
        var dto = ValidRequest();
        dto.RequestNumber = number;
        Assert.True(new CreateStudyRequestCommandValidator().Validate(new CreateStudyRequestCommand(dto)).IsValid);
        Assert.True(new UpdateStudyRequestCommandValidator().Validate(new UpdateStudyRequestCommand(1, dto)).IsValid);
    }

    [Fact]
    public void Mapping_and_normalization_preserve_the_entered_number_on_create_and_update()
    {
        var mapper = new MapperConfiguration(c => c.AddProfile<CentralStudiesProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var dto = ValidRequest();
        dto.RequestNumber = " ٠٠١/د/2026 ";
        var entity = mapper.Map<StudyRequest>(dto);
        StudyRequestSupport.Normalize(entity);
        Assert.Equal("٠٠١/د/2026", entity.RequestNumber);
        dto.RequestNumber = " 002/2026 ";
        mapper.Map(dto, entity);
        StudyRequestSupport.Normalize(entity);
        Assert.Equal("002/2026", entity.RequestNumber);
    }

    [Fact]
    public async Task Manual_numbers_cannot_be_reused_but_the_current_request_can_keep_its_number()
    {
        using var db = new DataContext(new DbContextOptionsBuilder<DataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var service = new StudyRequestService(db);
        var existing = await service.AddAsync(new StudyRequest { RequestNumber = "001/2026" });
        Assert.True(await service.NumberExistsAsync("001/2026"));
        Assert.False(await service.NumberExistsAsync("001/2026", existing.Id));
        Assert.True(await service.NumberExistsAsync("001/2026", existing.Id + 1));
        Assert.False(await service.NumberExistsAsync("002/2026"));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("0933123456", true)]          // الأرقام القديمة (هواتف) تبقى صالحة
    [InlineData("AB12", true)]
    [InlineData("م١٢٣", true)]                // حروف عربية وأرقام هندية
    [InlineData("ق-45/2026", true)]
    [InlineData("0933 123 456", true)]
    [InlineData("12#4", false)]
    [InlineData("<b>1</b>", false)]
    [InlineData("12345678901234567890123456789012345678901234567890", true)]
    [InlineData("123456789012345678901234567890123456789012345678901", false)]
    public void Follower_number_accepts_letters_and_digits(string? number, bool valid)
    {
        var dto = ValidRequest();
        dto.FollowerPhone = number;
        var errors = new StudyRequestFormValidator().Validate(dto).Errors;
        Assert.Equal(valid, errors.All(e => e.PropertyName != nameof(StudyRequestFormDto.FollowerPhone)));
    }

    [Fact]
    public void Follower_number_is_kept_as_typed_without_phone_normalization()
    {
        var request = new StudyRequest { FollowerPhone = "  ق-45/2026  " };
        StudyRequestSupport.Normalize(request);
        Assert.Equal("ق-45/2026", request.FollowerPhone);
        request.FollowerPhone = "   ";
        StudyRequestSupport.Normalize(request);
        Assert.Null(request.FollowerPhone);
    }

    private static StudyRequestFormDto ValidRequest() => new()
    {
        RequestNumber = "001/2026", RequestDate = new DateTime(2026, 11, 18),
        Urgency = 2, WorkType = 1, MainBeneficiaryId = 7, GovernorateCode = "SY02", StudyBranchId = 1
    };
}
