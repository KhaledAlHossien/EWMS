using Application.Features.CentralStudies;
using Domain.Entities.CentralStudiesBranch;
using Xunit;

namespace StudyBeneficiaries.Tests;

public class StudyProjectCodeTests
{
    private static StudyRequest Request(int studyBranchCode, string number, StudyWorkType workType, string? archive = null) => new()
    {
        // BranchId (فرع النظام) لا يدخل في الكود: الرقم الأول رمز فرع الدراسة
        BranchId = 42,
        StudyBranch = new StudyBranch { Code = studyBranchCode, Name = "فرع", Track = StudyTrack.Full },
        RequestNumber = number, WorkType = workType, AdminArchiveNumber = archive
    };

    [Theory]
    [InlineData(StudyWorkType.Construction, "N")]
    [InlineData(StudyWorkType.MaintenanceAndRestoration, "R")]
    [InlineData(StudyWorkType.ConstructionAndMaintenanceAndRestoration, "RN")]
    public void Uses_the_study_branch_code_the_year_and_saved_archive_with_the_requested_work_code(StudyWorkType workType, string workCode)
    {
        var request = Request(0, " 001/د ", workType, " 0009 ");
        var code = StudyRules.FormatProjectCode(request, new DateTime(2026, 1, 1), "9999");
        Assert.Equal($"0-26-{workCode}-001/د-0009", code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void The_first_segment_is_the_study_branch_code_not_the_system_branch_id(int studyBranchCode)
    {
        var request = Request(studyBranchCode, "77", StudyWorkType.Construction);
        Assert.Equal($"{studyBranchCode}-26-N-77-A1", StudyRules.FormatProjectCode(request, new DateTime(2026, 11, 28), "A1"));
    }

    [Fact]
    public void An_edited_draft_keeps_the_archive_number_stored_in_its_code_and_follows_the_new_year()
    {
        var request = Request(3, "12-ب", StudyWorkType.MaintenanceAndRestoration);
        var saved = new DateTime(2026, 12, 30);
        var code = StudyRules.FormatProjectCode(request, saved, "77-أ");

        var archive = StudyRules.ArchiveNumberFromCode(request, saved, code);
        Assert.Equal("77-أ", archive);
        Assert.Equal("3-27-R-12-ب-77-أ", StudyRules.FormatProjectCode(request, new DateTime(2027, 1, 2), archive));
    }

    [Theory]
    [InlineData("PRJ-OLD-1")]
    [InlineData("3-25-R-12-ب-77")]
    [InlineData(null)]
    public void A_code_that_does_not_match_the_saved_date_or_format_gives_no_archive_number(string? code)
    {
        var request = Request(3, "12-ب", StudyWorkType.MaintenanceAndRestoration);
        Assert.Null(StudyRules.ArchiveNumberFromCode(request, new DateTime(2026, 5, 1), code));
    }

    [Fact]
    public void An_unarchived_request_needs_an_archive_number_before_a_code_can_be_generated()
    {
        var request = Request(2, "001", StudyWorkType.Construction);
        var date = new DateTime(2006, 1, 1);
        Assert.Throws<InvalidOperationException>(() => StudyRules.FormatProjectCode(request, date, " "));
        Assert.Equal("2-06-N-001-0009", StudyRules.FormatProjectCode(request, date, " 0009 "));
    }

    [Fact]
    public void A_request_without_its_study_branch_cannot_generate_a_code()
    {
        var request = new StudyRequest { BranchId = 1, RequestNumber = "1", WorkType = StudyWorkType.Construction };
        Assert.Throws<InvalidOperationException>(() => StudyRules.FormatProjectCode(request, new DateTime(2026, 1, 1), "9"));
    }
}
