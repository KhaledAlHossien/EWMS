using Application.Features.CentralStudies.Assignments;
using Domain.Entities.CentralStudiesBranch;
using Xunit;

namespace StudyBeneficiaries.Tests;

public class StudyProjectCodeTests
{
    [Theory]
    [InlineData(StudyWorkType.Construction, "N")]
    [InlineData(StudyWorkType.MaintenanceAndRestoration, "R")]
    [InlineData(StudyWorkType.ConstructionAndMaintenanceAndRestoration, "RN")]
    public void Uses_the_assignment_year_and_saved_archive_with_the_requested_work_code(StudyWorkType workType, string workCode)
    {
        var request = new StudyRequest
        {
            BranchId = 7, RequestDate = new DateTime(2025, 12, 1),
            RequestNumber = " 001/د ", WorkType = workType, AdminArchiveNumber = " 0009 "
        };
        var code = StudyAssignmentSupport.FormatProjectCode(request, new DateTime(2026, 1, 1), "9999");
        Assert.Equal($"7-26-{workCode}-001/د-0009", code);
    }

    [Fact]
    public void An_unarchived_request_needs_an_archive_number_before_a_code_can_be_generated()
    {
        var request = new StudyRequest { BranchId = 2, RequestNumber = "001", WorkType = StudyWorkType.Construction };
        var date = new DateTime(2006, 1, 1);
        Assert.Throws<InvalidOperationException>(() => StudyAssignmentSupport.FormatProjectCode(request, date, " "));
        Assert.Equal("2-06-N-001-0009", StudyAssignmentSupport.FormatProjectCode(request, date, " 0009 "));
    }
}
