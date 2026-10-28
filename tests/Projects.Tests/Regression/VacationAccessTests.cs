using Application.Common;
using Application.Features.Vacations;
using Domain.Entities;
using Domain.Enums;

namespace Projects.Tests.Regression;

/// <summary>من يرى الإجازة ومن يقرر عليها: مرحلتان بصلاحيتين، حدّ الفرع، منع الموافقة الذاتية، وفصل المرحلتين عن الشخص نفسه</summary>
public class VacationAccessTests
{
    private const int Me = 2, Owner = 3, Other = 4;

    private static User UserOf(int id, int? branch = 1, int? department = 10, string role = "Emp") => new()
    {
        Id = id, FullName = "u" + id, Email = $"u{id}@x.sy", PasswordHash = "x", RoleId = 1,
        Role = new Role { Id = 1, Name = role }, BranchId = branch, DepartmentId = department
    };

    private static Viewer ViewerOf(User user, params string[] permissions) => new(user, new HashSet<string>(permissions));

    private static Vacation VacationOf(VacationStatus status, int userId = Owner, int branch = 1, int department = 10, int? firstApprover = null) => new()
    {
        Id = 1, UserId = userId, BranchId = branch, DepartmentId = department, VacationTypeId = 1,
        Status = status, FirstApprovedByUserId = firstApprover
    };

    // ───────── القرار ─────────

    [Fact]
    public void The_first_stage_needs_the_first_approval_permission()
    {
        var vacation = VacationOf(VacationStatus.PendingManager);

        Assert.Throws<UnauthorizedAccessException>(() => VacationAccess.EnsureCanDecide(ViewerOf(UserOf(Me)), vacation));
        Assert.Throws<UnauthorizedAccessException>(() =>
            VacationAccess.EnsureCanDecide(ViewerOf(UserOf(Me), AppPermissions.ApproveVacationFinal), vacation));

        VacationAccess.EnsureCanDecide(ViewerOf(UserOf(Me), AppPermissions.ApproveVacationFirst), vacation);
    }

    [Fact]
    public void The_final_stage_needs_the_final_approval_permission()
    {
        var vacation = VacationOf(VacationStatus.PendingBranchManager, firstApprover: Other);

        Assert.Throws<UnauthorizedAccessException>(() =>
            VacationAccess.EnsureCanDecide(ViewerOf(UserOf(Me), AppPermissions.ApproveVacationFirst), vacation));

        VacationAccess.EnsureCanDecide(ViewerOf(UserOf(Me), AppPermissions.ApproveVacationFinal), vacation);
    }

    [Fact]
    public void An_approver_of_another_branch_cannot_decide()
    {
        var vacation = VacationOf(VacationStatus.PendingManager, branch: 1);
        var viewer = ViewerOf(UserOf(Me, branch: 2), AppPermissions.ApproveVacationFirst);

        var error = Assert.Throws<UnauthorizedAccessException>(() => VacationAccess.EnsureCanDecide(viewer, vacation));
        Assert.Contains("خارج فرعك", error.Message);
    }

    [Fact]
    public void Nobody_approves_their_own_vacation_except_the_super_admin()
    {
        var vacation = VacationOf(VacationStatus.PendingManager, userId: Me);
        var approver = ViewerOf(UserOf(Me), AppPermissions.ApproveVacationFirst, AppPermissions.ApproveVacationFinal);

        Assert.Throws<UnauthorizedAccessException>(() => VacationAccess.EnsureCanDecide(approver, vacation));

        VacationAccess.EnsureCanDecide(ViewerOf(UserOf(Me, role: "SuperAdmin")), vacation);
    }

    [Fact]
    public void Whoever_gave_the_first_approval_cannot_also_give_the_final_one()
    {
        var vacation = VacationOf(VacationStatus.PendingBranchManager, firstApprover: Me);
        var viewer = ViewerOf(UserOf(Me), AppPermissions.ApproveVacationFirst, AppPermissions.ApproveVacationFinal);

        var error = Assert.Throws<UnauthorizedAccessException>(() => VacationAccess.EnsureCanDecide(viewer, vacation));
        Assert.Contains("المرحلة الأولى", error.Message);
    }

    [Theory]
    [InlineData(VacationStatus.Approved)]
    [InlineData(VacationStatus.Rejected)]
    [InlineData(VacationStatus.Cancelled)]
    public void A_request_that_is_not_pending_accepts_no_decision(VacationStatus status)
    {
        var viewer = ViewerOf(UserOf(Me), AppPermissions.ApproveVacationFirst, AppPermissions.ApproveVacationFinal);

        Assert.Throws<InvalidOperationException>(() => VacationAccess.EnsureCanDecide(viewer, VacationOf(status)));
    }

    [Fact]
    public void Each_stage_maps_to_its_own_permission_and_closed_states_to_none()
    {
        Assert.Equal(AppPermissions.ApproveVacationFirst, VacationAccess.PermissionForStage(VacationStatus.PendingManager));
        Assert.Equal(AppPermissions.ApproveVacationFinal, VacationAccess.PermissionForStage(VacationStatus.PendingBranchManager));
        Assert.Null(VacationAccess.PermissionForStage(VacationStatus.Approved));
    }

    // ───────── الرؤية ─────────

    [Fact]
    public void An_employee_sees_only_their_own_vacation()
    {
        var vacation = VacationOf(VacationStatus.PendingManager, userId: Owner);

        Assert.True(VacationAccess.CanView(ViewerOf(UserOf(Owner)), vacation));
        Assert.False(VacationAccess.CanView(ViewerOf(UserOf(Me), AppPermissions.ViewVacations), vacation));
    }

    [Fact]
    public void The_department_permission_opens_only_the_same_department()
    {
        var viewer = ViewerOf(UserOf(Me, department: 10), AppPermissions.ViewDepartmentVacations);

        Assert.True(VacationAccess.CanView(viewer, VacationOf(VacationStatus.Approved, department: 10)));
        Assert.False(VacationAccess.CanView(viewer, VacationOf(VacationStatus.Approved, department: 11)));
    }

    [Fact]
    public void The_branch_permission_opens_only_the_same_branch()
    {
        var viewer = ViewerOf(UserOf(Me, branch: 1), AppPermissions.ViewBranchVacations);

        Assert.True(VacationAccess.CanView(viewer, VacationOf(VacationStatus.Approved, branch: 1, department: 99)));
        Assert.False(VacationAccess.CanView(viewer, VacationOf(VacationStatus.Approved, branch: 2)));
    }

    [Fact]
    public void An_approver_sees_the_vacations_of_their_branch_to_decide()
    {
        var viewer = ViewerOf(UserOf(Me, branch: 1), AppPermissions.ApproveVacationFirst);

        Assert.True(VacationAccess.CanView(viewer, VacationOf(VacationStatus.PendingManager, branch: 1)));
        Assert.False(VacationAccess.CanView(viewer, VacationOf(VacationStatus.PendingManager, branch: 2)));
    }

    [Fact]
    public void A_user_with_no_unit_matches_no_record_even_when_the_record_has_none()
    {
        // null == null يجب ألا يمرّ: مستخدم بلا فرع ولا قسم لا يرى إجازة بفرع وقسم صفريين
        var viewer = ViewerOf(UserOf(Me, branch: null, department: null), AppPermissions.ViewBranchVacations, AppPermissions.ViewDepartmentVacations);
        var vacation = new Vacation { Id = 1, UserId = Owner, BranchId = 0, DepartmentId = 0, VacationTypeId = 1 };

        Assert.False(VacationAccess.CanView(viewer, vacation));
    }

    [Fact]
    public void The_super_admin_sees_everything()
    {
        Assert.True(VacationAccess.CanView(ViewerOf(UserOf(Me, role: "SuperAdmin", branch: null, department: null)), VacationOf(VacationStatus.Approved, branch: 7)));
    }

    [Fact]
    public void Viewing_another_users_page_follows_the_same_boundaries()
    {
        var target = UserOf(Owner, branch: 1, department: 10);

        Assert.True(VacationAccess.CanViewUser(ViewerOf(UserOf(Owner)), target));
        Assert.False(VacationAccess.CanViewUser(ViewerOf(UserOf(Me), AppPermissions.ViewVacations), target));
        Assert.True(VacationAccess.CanViewUser(ViewerOf(UserOf(Me, department: 10), AppPermissions.ViewDepartmentVacations), target));
        Assert.False(VacationAccess.CanViewUser(ViewerOf(UserOf(Me, department: 11), AppPermissions.ViewDepartmentVacations), target));
        Assert.True(VacationAccess.CanViewUser(ViewerOf(UserOf(Me, branch: 1, department: 11), AppPermissions.ViewBranchVacations), target));
    }
}
