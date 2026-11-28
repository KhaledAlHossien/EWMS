using System.Security.Claims;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.CentralStudies;
using Application.Features.CentralStudies.Approvals.Commands.Decide;
using Application.Features.CentralStudies.DiwanTransactions;
using Application.Features.CentralStudies.DiwanTransactions.Commands.Create;
using Application.Features.CentralStudies.DiwanTransactions.Commands.Submit;
using Application.Features.CentralStudies.DiwanTransactions.Commands.Update;
using Application.Features.CentralStudies.StartOrders;
using Application.Features.CentralStudies.StartOrders.Commands.Create;
using Application.Helper.Profiles;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Entities.CentralStudiesBranch;
using Infrastructure.Files;
using Infrastructure.Persistence.Data;
using Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace StudyBeneficiaries.Tests;

/// <summary>معاملة الديوان مستنداً (DWN): إنشاء بالصادر، الرد المركزي ومرفقه، توقيع رئيس الفرع، وأثر القرار المركزي</summary>
public sealed class StudyDiwanWorkflowTests : IDisposable
{
    private const int Clerk = 11;
    private const int Head = 12;
    private const int ProjectOffice = 13;
    private const int OtherBranch = 14;
    private const int Admin = 15;
    private const int FollowingEditor = 16;
    private static readonly DateTime Date = new(2026, 11, 19);
    private readonly string _filesRoot = Path.Combine(Path.GetTempPath(), "ewms-diwan-test-" + Guid.NewGuid());
    private readonly DataContext _db = new(new DbContextOptionsBuilder<DataContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<CentralStudiesProfile>(), NullLoggerFactory.Instance).CreateMapper();
    private readonly SilentPusher _pusher = new();

    [Theory]
    [InlineData(StudyRecommendation.Modify, StudyDiwanDecision.Approved, StudyDocumentStatus.Approved, true)]
    [InlineData(StudyRecommendation.Accept, StudyDiwanDecision.Rejected, StudyDocumentStatus.Rejected, false)]
    [InlineData(StudyRecommendation.NotAvailable, StudyDiwanDecision.Approved, StudyDocumentStatus.Approved, false)]
    public async Task Needs_the_central_reply_its_letter_and_the_branch_head_signature_before_a_start_order(
        StudyRecommendation recommendation, StudyDiwanDecision decision, StudyDocumentStatus finalStatus, bool startAllowed)
    {
        await SeedAsync(recommendation);
        var (headKit, _) = Viewer(Head);
        await headKit.NotifyNextStepAsync(StudyEntityType.Response, 1);
        Assert.Contains(_db.Notifications, n => n.UserId == Clerk);   // من يملك إنشاء المعاملة في الفرع

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Create(OtherBranch, "0004"));
        var created = await Create(Clerk, "0004");
        Assert.Equal($"DWN-{DateTime.Today.Year % 100:D2}-0001", created.TransactionNumber);
        Assert.Equal((int)StudyDocumentStatus.Draft, created.Status);
        Assert.Equal(created.Id, created.Chain!.DiwanId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(Clerk, "0005"));   // معاملة واحدة لكل رد

        await Assert.ThrowsAsync<InvalidOperationException>(() => Submit(Clerk, created.Id));          // لا رد مركزي بعد
        var updated = await Update(Clerk, created.Id, "0004", "0007", decision);
        Assert.Equal("0007", updated.ReplyNumber);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Submit(Clerk, created.Id));          // لا صورة للكتاب
        AttachLetter(created.Id);
        var pending = await Submit(Clerk, created.Id);
        Assert.Equal((int)StudyDocumentStatus.PendingApproval, pending.Status);
        var row = Assert.Single(pending.Approvals);
        Assert.Equal((int)StudySignerRole.BranchHead, row.SignerRole);
        Assert.Contains(_db.Notifications, n => n.UserId == Head && n.Type == Domain.Enums.NotificationType.StudyApprovalRequested);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateOrder(created.Id));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Decide(Clerk, row.Id));            // لا يملك صفة رئيس الفرع
        await Decide(Head, row.Id);

        var transaction = await _db.StudyDiwanTransactions.Include(t => t.Response).AsNoTracking().SingleAsync();
        Assert.Equal(finalStatus, transaction.Status);
        // الرفض المركزي يعيد الرد إلى القسم المتابع للتعديل ويُشعر من يعدّله؛ غيره يبقى الرد معتمداً
        var centralRejection = decision == StudyDiwanDecision.Rejected;
        Assert.Equal(centralRejection ? StudyDocumentStatus.Rejected : StudyDocumentStatus.Approved, transaction.Response.Status);
        Assert.Equal(centralRejection, _db.Notifications.Any(n => n.UserId == FollowingEditor));
        Assert.Equal(startAllowed, _db.Notifications.Any(n => n.UserId == ProjectOffice));

        if (!startAllowed)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateOrder(created.Id));
            return;
        }
        var order = await CreateOrder(created.Id);
        Assert.Equal(created.Id, order.DiwanTransactionId);
        Assert.Equal(created.TransactionNumber, order.DiwanTransactionNumber);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateOrder(created.Id));           // أمر واحد لكل رد
    }

    [Fact]
    public async Task After_a_central_rejection_the_same_transaction_is_updated_and_sent_again_once_the_response_is_approved()
    {
        await SeedAsync(StudyRecommendation.Accept);
        var created = await Create(Clerk, "0004");
        await Update(Clerk, created.Id, "0004", "0007", StudyDiwanDecision.Rejected);
        AttachLetter(created.Id);
        var row = Assert.Single((await Submit(Clerk, created.Id)).Approvals);
        await Decide(Head, row.Id);

        // الرد عاد إلى القسم المتابع: لا تُحدَّث المعاملة قبل اعتماده المعدَّل
        await Assert.ThrowsAsync<InvalidOperationException>(() => Update(Clerk, created.Id, "0005", "0009", StudyDiwanDecision.Approved));

        var response = await _db.StudyAssignmentResponses.SingleAsync();
        response.Status = StudyDocumentStatus.Approved;   // القسم المتابع عدّل الرد وأعاد اعتماده (التواقيع خارج هذا الاختبار)
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var (headKit, _) = Viewer(Head);
        var before = _db.Notifications.Count(n => n.UserId == Clerk);
        await headKit.NotifyNextStepAsync(StudyEntityType.Response, 1);
        Assert.True(_db.Notifications.Count(n => n.UserId == Clerk) > before);   // يحدّث المعاملة نفسها

        var again = await Update(Clerk, created.Id, "0005", "0009", StudyDiwanDecision.Approved);
        Assert.Equal(created.Id, again.Id);
        Assert.Equal(("0005", "0009"), (again.DispatchNumber, again.ReplyNumber));
        var second = Assert.Single((await Submit(Clerk, created.Id)).Approvals);
        await Decide(Head, second.Id);

        Assert.Equal(1, await _db.StudyDiwanTransactions.CountAsync());
        var order = await CreateOrder(created.Id);
        Assert.Equal("0009", (await _db.StudyDiwanTransactions.SingleAsync()).ReplyNumber);
        Assert.Equal(created.Id, order.DiwanTransactionId);
    }

    [Fact]
    public async Task A_system_admin_without_a_branch_can_register_a_transaction_in_any_branch()
    {
        await SeedAsync(StudyRecommendation.Modify);
        var created = await Create(Admin, "0100");   // 4-أ: الحد StudyRules.InScope لا «في فرعه»
        Assert.Equal((int)StudyDocumentStatus.Draft, created.Status);
        Assert.True(created.CanEdit);
    }

    [Fact]
    public async Task A_committed_letter_is_removed_when_its_record_is_not_saved()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["FileStorage:Root"] = _filesRoot }).Build();
        var store = new FileStore(new FileStorageSettings(config));
        await using var staged = await store.StageAsync(FileAreas.Studies, "documents/1",
            (stream, ct) => stream.WriteAsync("%PDF-1.7\n"u8.ToArray(), ct).AsTask());
        await staged.CommitAsync();
        await using (var read = store.OpenRead(staged.Key)) Assert.True(read.Length > 0);

        await staged.RevertAsync();
        Assert.Throws<KeyNotFoundException>(() => { store.OpenRead(staged.Key).Dispose(); });
    }

    [Theory]
    [InlineData("Cannot insert duplicate key row in object 'dbo.StudyRequests' with unique index 'IX_StudyRequests_RequestNumber'. The duplicate key value is (301/د/2026).", "رقم طلب الدراسة «301/د/2026»")]
    [InlineData("Cannot insert duplicate key row in object 'dbo.StudyAssignments' with unique index 'IX_StudyAssignments_ProjectCode'. The duplicate key value is (1-26-N-12-9).", "كود المشروع «1-26-N-12-9»")]
    [InlineData("Cannot insert duplicate key row in object 'dbo.StudyDiwanTransactions' with unique index 'IX_StudyDiwanTransactions_ResponseId'. The duplicate key value is (1).", "لإشعار الرد معاملة ديوان مسبقاً")]
    [InlineData("Violation of UNIQUE KEY constraint 'IX_Other'.", "القيمة مستخدمة في سجل آخر")]
    public void A_concurrent_duplicate_names_the_field_and_the_value(string sql, string expected) =>
        Assert.StartsWith(expected, StudyRepositoryBase.DuplicateMessage(sql));

    // ════════════════════ استدعاء المعالجات كما يستدعيها MediatR ════════════════════

    private Task<StudyDiwanTransactionDto> Create(int user, string dispatch)
    {
        var (kit, _) = Viewer(user);
        var service = new StudyDiwanTransactionService(_db);
        return new CreateStudyDiwanTransactionCommandHandler(service, new StudyResponseService(_db), new FixedNumber(), kit, Support(service, kit))
            .Handle(new(new StudyDiwanTransactionFormDto { ResponseId = 1, DispatchNumber = dispatch, DispatchDate = Date }), default);
    }

    private async Task<StudyDiwanTransactionDto> Update(int user, int id, string dispatch, string reply, StudyDiwanDecision decision)
    {
        var (kit, _) = Viewer(user);
        var service = new StudyDiwanTransactionService(_db);
        var current = await _db.StudyDiwanTransactions.AsNoTracking().SingleAsync(t => t.Id == id);
        var result = await new UpdateStudyDiwanTransactionCommandHandler(service, kit, Support(service, kit)).Handle(new(id, new StudyDiwanTransactionFormDto
        {
            DispatchNumber = dispatch, DispatchDate = Date, ReplyNumber = reply, ReplyDate = Date.AddDays(5),
            CentralDecision = (int)decision, RowVersion = current.RowVersion
        }), default);
        _db.ChangeTracker.Clear();
        return result;
    }

    private async Task<StudyDiwanTransactionDto> Submit(int user, int id)
    {
        var (kit, _) = Viewer(user);
        var service = new StudyDiwanTransactionService(_db);
        var result = await new SubmitStudyDiwanTransactionCommandHandler(service, new StudyAttachmentService(_db), kit, Support(service, kit))
            .Handle(new(id), default);
        _db.ChangeTracker.Clear();
        return result;
    }

    private async Task Decide(int user, int approvalId)
    {
        var (kit, _) = Viewer(user);
        await new DecideStudyApprovalCommandHandler(new StudyApprovalService(_db), new UserSignatureService(_db), kit)
            .Handle(new(approvalId, new StudyDecisionDto { Decision = (int)StudyDecision.Approved }), default);
        _db.ChangeTracker.Clear();
    }

    private Task<StudyStartOrderDto> CreateOrder(int transactionId)
    {
        var (kit, users) = Viewer(ProjectOffice);
        var orders = new StudyStartOrderService(_db);
        var support = new StudyStartOrderSupport(orders, new StudyLookupService(_db), new DepartmentService(_db), users, kit);
        var handler = new CreateStudyStartOrderCommandHandler(orders, new StudyDiwanTransactionService(_db), new FixedNumber(), kit, support);
        return handler.Handle(new(new StudyStartOrderFormDto
        {
            DiwanTransactionId = transactionId, CompetentDepartmentId = 1, ProjectManagerUserId = ProjectOffice,
            OrderDate = Date, StudyStartDate = Date, StudyDeadline = Date.AddDays(7)
        }), default);
    }

    /// <summary>صورة كتاب الرد المركزي: مرفق عادي على المعاملة (يرفعه في التطبيق StudyAttachments/Upload)</summary>
    private void AttachLetter(int transactionId)
    {
        _db.StudyAttachments.Add(new StudyAttachment
        {
            DiwanTransactionId = transactionId, AttachmentTypeId = 1, UploadedByUserId = Clerk, FileName = "reply.pdf",
            ContentType = "application/pdf", Size = 9, StorageKey = "studies/documents/1/" + Guid.NewGuid().ToString("N"), UploadedAt = DateTime.Now
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private sealed class NoLiveSessions : ILiveSessions
    {
        public void CloseUser(int userId) { }
        public void CloseToken(string tokenHash) { }
    }

    private static StudyDiwanTransactionSupport Support(IStudyDiwanTransactionService service, StudyKit kit) => new(service, kit);

    private (StudyKit Kit, UserService Users) Viewer(int id)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, id.ToString())])) };
        var users = new UserService(_db, new HttpContextAccessor { HttpContext = context }, new NoLiveSessions());
        return (new StudyKit(users, new UserPermissionService(_db), new StudyActivityLogService(_db),
            new StudyApprovalService(_db), new NotificationService(_db, _pusher), new StudyRequestService(_db),
            new StudyAssignmentService(_db), new StudyResponseService(_db), new StudyStartOrderService(_db),
            new StudyDiwanTransactionService(_db), null!, null!, _mapper), users);
    }

    private async Task SeedAsync(StudyRecommendation recommendation)
    {
        _db.Branches.AddRange(new Branch { Id = 1, Name = "الدراسات" }, new Branch { Id = 2, Name = "فرع آخر" });
        _db.Departments.Add(new Department { Id = 1, BranchId = 1, Name = "قسم إدارة المشاريع" });
        _db.StudyAttachmentTypes.Add(new StudyAttachmentType { Id = 1, Name = "كتاب رسمي", IsActive = true });
        string[] names = [StudyPermissions.ViewDiwan, StudyPermissions.CreateDiwan, StudyPermissions.EditDiwan,
            "SignStudyAsBranchHead", "CreateStudyStartOrder", StudyPermissions.EditResponse];
        _db.Permissions.AddRange(names.Select((name, i) => new Permission { Id = i + 1, Name = name }));
        AddUser(Clerk, 1, null, [1, 2, 3]);
        AddUser(Head, 1, null, [1, 4]);
        AddUser(ProjectOffice, 1, null, [1, 5]);
        AddUser(OtherBranch, 2, null, [1, 2, 3]);
        AddUser(FollowingEditor, 1, 1, [6]);
        AddUser(Admin, null, null, [1, 2, 3], "SuperAdmin");
        _db.StudyBranches.Add(new StudyBranch { Id = 1, BranchId = 1, Code = 0, Name = "المركزية", Track = StudyTrack.Full, IsActive = true });
        _db.StudyRequests.Add(new StudyRequest { Id = 1, BranchId = 1, StudyBranchId = 1, CreatedByUserId = Clerk, Status = StudyDocumentStatus.Approved, MainBeneficiary = "جهة" });
        _db.StudyAssignments.Add(new StudyAssignment { Id = 1, RequestId = 1, FollowingDepartmentId = 1, CreatedByUserId = Clerk, Status = StudyDocumentStatus.Approved });
        _db.StudyAssignmentResponses.Add(new StudyAssignmentResponse
        {
            Id = 1, ResponseNumber = "RSP-26-0014", AssignmentId = 1,
            CreatedByUserId = Clerk, Status = StudyDocumentStatus.Approved, Recommendation = recommendation
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    private void AddUser(int id, int? branchId, int? departmentId, int[] permissions, string? role = null)
    {
        _db.Roles.Add(new Role { Id = id, Name = role ?? $"Role-{id}" });
        _db.Users.Add(new User
        {
            Id = id, RoleId = id, BranchId = branchId, DepartmentId = departmentId, FullName = $"User {id}",
            Email = $"user{id}@test.invalid", PasswordHash = "test", IsActive = true
        });
        _db.RolePermissions.AddRange(permissions.Select(p => new RolePermission { RoleId = id, PermissionId = p }));
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_filesRoot)) Directory.Delete(_filesRoot, recursive: true);
    }

    private sealed class SilentPusher : INotificationPusher
    {
        public Task PushAsync(IReadOnlyCollection<Notification> notifications) => Task.CompletedTask;
    }

    private sealed class FixedNumber : IStudyNumberService
    {
        public Task<int> NextAsync(StudyDocumentType type, int year) => Task.FromResult(1);
    }
}
