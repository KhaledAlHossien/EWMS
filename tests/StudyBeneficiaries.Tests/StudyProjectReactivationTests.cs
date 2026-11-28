using Application.Common;
using Application.DTOs.Response;
using Application.Features.CentralStudies;
using Application.Features.CentralStudies.Projects;
using Domain.Entities;
using Domain.Entities.CentralStudiesBranch;
using Domain.Enums;
using Infrastructure.Persistence.Data;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace StudyBeneficiaries.Tests;

public class StudyProjectReactivationTests
{
    [Theory]
    [InlineData(StudyProjectStatus.Cancelled, true, true, 1, true)]
    [InlineData(StudyProjectStatus.Cancelled, false, true, 1, false)]
    [InlineData(StudyProjectStatus.Cancelled, true, false, 1, false)]
    [InlineData(StudyProjectStatus.Cancelled, true, true, 2, false)]
    [InlineData(StudyProjectStatus.Running, true, true, 1, false)]
    [InlineData(StudyProjectStatus.Suspended, true, true, 1, false)]
    [InlineData(StudyProjectStatus.Completed, true, true, 1, false)]
    public void Requires_a_cancelled_project_with_an_approved_order_and_permission_in_scope(
        StudyProjectStatus status, bool approvedOrder, bool hasPermission, int viewerBranchId, bool canReactivate)
    {
        var user = new User { BranchId = viewerBranchId, FullName = "Test user", Email = "test@example.invalid", PasswordHash = "test", RoleId = 1 };
        var viewer = new Viewer(user,
            hasPermission ? new HashSet<string> { StudyPermissions.CancelDocuments } : new HashSet<string>());
        var dto = new StudyProjectDto
        {
            Status = (int)status, BranchId = 1,
            StartOrderStatus = (int)(approvedOrder ? StudyDocumentStatus.Approved : StudyDocumentStatus.Cancelled)
        };

        StudyProjectSupport.ApplyFlags(dto, viewer);

        Assert.Equal(canReactivate, dto.CanReactivate);
    }

    [Theory]
    [InlineData(StudyDecision.Approved, true)]
    [InlineData(StudyDecision.Rejected, false)]
    public async Task Notifies_only_the_manager_open_task_recipients_and_eligible_signers_once(
        StudyDecision firstDecision, bool resumesApproval)
    {
        await using var db = new DataContext(new DbContextOptionsBuilder<DataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Roles.AddRange(new Role { Id = 1, Name = "Employee" }, new Role { Id = 2, Name = "Department head" },
            new Role { Id = 3, Name = "Branch signer" }, new Role { Id = 4, Name = "Project management signer" });
        db.Permissions.AddRange(new Permission { Id = 1, Name = StudyPermissions.AddProjectMember },
            new Permission { Id = 2, Name = StudyRules.SignPermission(StudySignerRole.BranchHead) },
            new Permission { Id = 3, Name = StudyRules.SignPermission(StudySignerRole.ProjectManagement) });
        db.RolePermissions.AddRange(new RolePermission { RoleId = 2, PermissionId = 1 },
            new RolePermission { RoleId = 3, PermissionId = 2 }, new RolePermission { RoleId = 4, PermissionId = 3 });
        db.Users.AddRange(
            Employee(1, role: 3), Employee(2), Employee(3, role: 2, department: 1), Employee(4, role: 2, department: 2),
            Employee(5, role: 2, department: 3), Employee(6, role: 3), Employee(7, role: 4),
            Employee(8, active: false), Employee(9), Employee(10, branch: 2), Employee(11, role: 3, branch: 2),
            Employee(12, role: 2, department: 4));
        db.Departments.AddRange(Enumerable.Range(1, 4).Select(id => new Department { Id = id, BranchId = 1 }));
        db.Offices.Add(new Office { Id = 1, DepartmentId = 2 });

        var project = new StudyProject
        {
            Id = 1, BranchId = 1, Code = "P-1", ManagerUserId = 1, Status = StudyProjectStatus.Cancelled,
            Departments = [new() { DepartmentId = 1 }, new() { DepartmentId = 2 }, new() { DepartmentId = 3 }],
            Stages = [new ProjectStage
            {
                Status = StudyStageStatus.InProgress,
                Tasks =
                [
                    new() { TargetType = StudyTaskTarget.User, AssigneeUserId = 2, Status = StudyTaskStatus.InProgress },
                    new() { TargetType = StudyTaskTarget.User, AssigneeUserId = 2, Status = StudyTaskStatus.NotStarted },
                    new() { TargetType = StudyTaskTarget.User, AssigneeUserId = 8, Status = StudyTaskStatus.NotStarted },
                    new() { TargetType = StudyTaskTarget.User, AssigneeUserId = 9, Status = StudyTaskStatus.Done },
                    new() { TargetType = StudyTaskTarget.User, AssigneeUserId = 10, Status = StudyTaskStatus.NotStarted },
                    new() { TargetType = StudyTaskTarget.Department, DepartmentId = 1, Status = StudyTaskStatus.NotStarted },
                    new() { TargetType = StudyTaskTarget.Office, OfficeId = 1, Status = StudyTaskStatus.InProgress },
                    new() { TargetType = StudyTaskTarget.Department, DepartmentId = 3, Status = StudyTaskStatus.Done },
                    new() { TargetType = StudyTaskTarget.Department, DepartmentId = 4, Status = StudyTaskStatus.NotStarted }
                ]
            }]
        };
        db.StudyProjects.Add(project);
        var approvals = StudyRules.BuildApprovalRows(StudyEntityType.Project, project.Id);
        approvals[0].Decision = firstDecision;
        approvals[0].SignerUserId = 7;
        db.StudyApprovals.AddRange(approvals);
        await db.SaveChangesAsync();

        var kit = new StudyKit(null!, new UserPermissionService(db), null!, new StudyApprovalService(db), null!,
            null!, null!, null!, null!, null!, new StudyProjectService(db), null!, null!);
        var notes = await kit.ProjectReactivationNotificationsAsync(project, 1, default);

        int[] expectedRecipients = resumesApproval ? [1, 2, 3, 4, 6] : [1, 2, 3, 4];
        Assert.Equal(expectedRecipients, notes.Select(n => n.UserId).Order());
        Assert.All(notes, n =>
        {
            Assert.Equal(NotificationType.StudyProjectReactivated, n.Type);
            Assert.Equal(project.Id, n.RelatedEntityId);
            Assert.Equal(resumesApproval && n.UserId is 1 or 6 ? "StudyProjectApproval" : "StudyProject", n.RelatedEntityType);
        });
    }

    private static User Employee(int id, int role = 1, int? department = null, int branch = 1, bool active = true) => new()
    {
        Id = id, FullName = $"User {id}", Email = $"user{id}@example.invalid", PasswordHash = "test",
        RoleId = role, DepartmentId = department, BranchId = branch, IsActive = active
    };
}
