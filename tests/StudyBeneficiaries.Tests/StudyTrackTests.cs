using Application.Common;
using Application.DTOs.Response;
using Application.Features.CentralStudies;
using Application.Features.CentralStudies.Projects;
using Domain.Entities;
using Domain.Entities.CentralStudiesBranch;
using Xunit;

namespace StudyBeneficiaries.Tests;

/// <summary>مسار فرع الدراسة: المركزية بالدورة الكاملة، والأفرع الجغرافية بالإحالة والتنسيق (2026-11-28)</summary>
public class StudyTrackTests
{
    [Fact]
    public void Central_project_completion_is_signed_by_project_management_then_the_branch_head()
    {
        Assert.Equal([StudySignerRole.ProjectManagement, StudySignerRole.BranchHead],
            StudyRules.RequiredSigners(StudyEntityType.Project, StudyTrack.Full));
        Assert.Equal(StudyRules.RequiredSigners(StudyEntityType.Project), StudyRules.RequiredSigners(StudyEntityType.Project, StudyTrack.Full));
    }

    [Fact]
    public void Regional_project_completion_is_signed_by_coordination_then_quality_control()
    {
        var rows = StudyRules.BuildApprovalRows(StudyEntityType.Project, 9, StudyTrack.Coordination);
        Assert.Equal([StudySignerRole.Coordination, StudySignerRole.QualityControl], rows.Select(r => r.SignerRole));
        Assert.Equal([1, 2], rows.Select(r => r.SortOrder));
        Assert.All(rows, r => Assert.Equal(9, r.ProjectId));
        Assert.Equal("SignStudyAsCoordination", StudyRules.SignPermission(StudySignerRole.Coordination));
        Assert.False(StudyRules.IsDepartmentRole(StudySignerRole.Coordination));
    }

    [Fact]
    public void The_track_only_changes_project_completion_signers()
    {
        foreach (var type in new[] { StudyEntityType.Assignment, StudyEntityType.Response, StudyEntityType.StartOrder,
                     StudyEntityType.DesignApproval, StudyEntityType.DiwanTransaction })
            Assert.Equal(StudyRules.RequiredSigners(type, StudyTrack.Full), StudyRules.RequiredSigners(type, StudyTrack.Coordination));
    }

    [Theory]
    [InlineData(StudyTrack.Full, false)]
    [InlineData(StudyTrack.Coordination, true)]
    public void Only_central_requests_get_an_internal_assignment(StudyTrack track, bool throws)
    {
        var request = new StudyRequest { StudyBranch = new StudyBranch { Name = "الوسطى", Code = 3, Track = track } };
        if (throws) Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureFullTrack(request));
        else StudyRules.EnsureFullTrack(request);
    }

    [Theory]
    [InlineData(StudyDocumentStatus.Approved, true)]
    [InlineData(StudyDocumentStatus.Cancelled, false)]
    public void A_cancelled_regional_project_is_reactivated_only_while_its_request_is_not_cancelled(StudyDocumentStatus requestStatus, bool canReactivate)
    {
        var viewer = new Viewer(new User { BranchId = 1, FullName = "u", Email = "u@example.invalid", PasswordHash = "x", RoleId = 1 },
            new HashSet<string> { StudyPermissions.CancelDocuments });
        var dto = new StudyProjectDto
        {
            Status = (int)StudyProjectStatus.Cancelled, BranchId = 1, Track = (int)StudyTrack.Coordination,
            StartOrderId = null, StartOrderStatus = 0, RequestStatus = (int)requestStatus
        };

        StudyProjectSupport.ApplyFlags(dto, viewer);

        Assert.Equal(canReactivate, dto.CanReactivate);
    }
}
