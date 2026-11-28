using System.Security.Claims;
using Application.Features.CentralStudies;
using Application.Features.CentralStudies.Projects;
using Application.Helper.Profiles;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace StudyBeneficiaries.Tests;

/// <summary>مدير مشروع الفرع الجغرافي رئيس قسم التنسيق والارتباط: من يملك التوقيع بصفته في فرع الدراسة وله قسم</summary>
public sealed class StudyRegionalManagerTests : IDisposable
{
    private const int Head = 21;
    private const int SecondHead = 22;
    private const int Employee = 23;
    private const int OtherBranchHead = 24;
    private const int Admin = 25;
    private const int HeadWithoutDepartment = 26;
    private readonly DataContext _db = new(new DbContextOptionsBuilder<DataContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<CentralStudiesProfile>(), NullLoggerFactory.Instance).CreateMapper();

    public StudyRegionalManagerTests()
    {
        _db.Branches.AddRange(new Branch { Id = 5, Name = "الدراسات" }, new Branch { Id = 6, Name = "فرع آخر" });
        _db.Departments.AddRange(new Department { Id = 18, BranchId = 5, Name = "التنسيق والارتباط" }, new Department { Id = 19, BranchId = 6, Name = "قسم" });
        _db.Permissions.Add(new Permission { Id = 1, Name = "SignStudyAsCoordination" });
        AddUser(Head, 5, 18, true, "عبير");
        AddUser(Employee, 5, 18, false, "موظف");
        AddUser(OtherBranchHead, 6, 19, true, "رئيس فرع آخر");
        AddUser(Admin, null, null, true, "المدير", "SuperAdmin");
        AddUser(HeadWithoutDepartment, 5, null, true, "بلا قسم");
        _db.SaveChanges();
    }

    [Fact]
    public async Task The_referrer_who_heads_coordination_manages_the_project()
    {
        var manager = await Support(Head).CoordinationManagerAsync(5, null, await Viewer(Head));
        Assert.Equal(Head, manager.Id);
        Assert.Equal(18, manager.DepartmentId);
    }

    [Fact]
    public async Task Without_a_choice_the_only_head_of_the_branch_is_the_manager()
    {
        Assert.Equal(Head, (await Support(Employee).CoordinationManagerAsync(5, null, await Viewer(Employee))).Id);
        Assert.Equal(Head, (await Support(Admin).CoordinationManagerAsync(5, null, await Viewer(Admin))).Id);   // المدير العام لا يُعد رئيساً
    }

    [Fact]
    public async Task A_chosen_manager_must_be_a_head_in_the_study_branch()
    {
        var viewer = await Viewer(Admin);
        Assert.Equal(Head, (await Support(Admin).CoordinationManagerAsync(5, Head, viewer)).Id);
        foreach (var id in new[] { Employee, OtherBranchHead, Admin, HeadWithoutDepartment })
            await Assert.ThrowsAsync<InvalidOperationException>(() => Support(Admin).CoordinationManagerAsync(5, id, viewer));
    }

    [Fact]
    public async Task Several_heads_require_a_choice_and_none_explains_the_permission()
    {
        AddUser(SecondHead, 5, 18, true, "باسل");
        await _db.SaveChangesAsync();
        var many = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Support(Employee).CoordinationManagerAsync(5, null, await Viewer(Employee)));
        Assert.Contains("باسل", many.Message);
        Assert.Contains("عبير", many.Message);

        // فرع آخر برئيس واحد ← هو المدير؛ وإن لم يبقَ رئيس نشط تشرح الرسالة الصلاحية المطلوبة
        Assert.Equal(OtherBranchHead, (await Support(Admin).CoordinationManagerAsync(6, null, await Viewer(Admin))).Id);
        _db.Users.Single(u => u.Id == OtherBranchHead).IsActive = false;
        await _db.SaveChangesAsync();
        var empty = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Support(Admin).CoordinationManagerAsync(6, null, await Viewer(Admin)));
        Assert.Contains("التوقيع بصفة التنسيق والارتباط", empty.Message);
    }

    private StudyProjectSupport Support(int user) => new(new StudyProjectService(_db), new StudyStageService(_db), Kit(user));

    private Task<Application.Common.Viewer> Viewer(int user) => Kit(user).ViewerAsync();

    private StudyKit Kit(int id)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, id.ToString())])) };
        var users = new UserService(_db, new HttpContextAccessor { HttpContext = context }, new NoLiveSessions());
        return new StudyKit(users, new UserPermissionService(_db), new StudyActivityLogService(_db), new StudyApprovalService(_db),
            new NotificationService(_db, new SilentPusher()), new StudyRequestService(_db), new StudyAssignmentService(_db),
            new StudyResponseService(_db), new StudyStartOrderService(_db), new StudyDiwanTransactionService(_db), null!, null!, _mapper);
    }

    private void AddUser(int id, int? branchId, int? departmentId, bool coordination, string name, string? role = null)
    {
        _db.Roles.Add(new Role { Id = id, Name = role ?? $"Role-{id}" });
        _db.Users.Add(new User
        {
            Id = id, RoleId = id, BranchId = branchId, DepartmentId = departmentId, FullName = name,
            Email = $"user{id}@test.invalid", PasswordHash = "test", IsActive = true
        });
        if (coordination) _db.RolePermissions.Add(new RolePermission { RoleId = id, PermissionId = 1 });
    }

    public void Dispose() => _db.Dispose();

    private sealed class NoLiveSessions : ILiveSessions
    {
        public void CloseUser(int userId) { }
        public void CloseToken(string tokenHash) { }
    }

    private sealed class SilentPusher : INotificationPusher
    {
        public Task PushAsync(IReadOnlyCollection<Notification> notifications) => Task.CompletedTask;
    }
}
