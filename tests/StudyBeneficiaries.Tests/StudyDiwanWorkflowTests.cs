using System.Security.Claims;
using System.Text;
using Application.DTOs.Request;
using Application.Features.CentralStudies;
using Application.Features.CentralStudies.Responses;
using Application.Features.CentralStudies.Responses.Commands.RegisterDiwanDispatch;
using Application.Features.CentralStudies.Responses.Commands.RegisterDiwanReply;
using Application.Features.CentralStudies.Responses.Commands.ReviewDiwanReply;
using Application.Features.CentralStudies.Responses.Queries.GetDiwanReplyFile;
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

public sealed class StudyDiwanWorkflowTests : IDisposable
{
    private const int Clerk = 11;
    private const int Head = 12;
    private const int ProjectOffice = 13;
    private const int OtherBranch = 14;
    private static readonly DateOnly Date = new(2026, 11, 19);
    private static readonly byte[] Version = [1, 0, 0, 0, 0, 0, 0, 0];
    private static readonly string EncodedVersion = Convert.ToBase64String(Version);
    private readonly string _filesRoot = Path.Combine(Path.GetTempPath(), "ewms-diwan-test-" + Guid.NewGuid());
    private readonly DataContext _db = new(new DbContextOptionsBuilder<DataContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<CentralStudiesProfile>(), NullLoggerFactory.Instance).CreateMapper();
    private readonly SilentPusher _pusher = new();

    [Theory]
    [InlineData(StudyRecommendation.Modify, StudyDiwanDecision.Approved, StudyDiwanStage.Referred, true)]
    [InlineData(StudyRecommendation.Accept, StudyDiwanDecision.Rejected, StudyDiwanStage.Rejected, false)]
    [InlineData(StudyRecommendation.NotAvailable, StudyDiwanDecision.Approved, StudyDiwanStage.ClosedWithoutStart, false)]
    public async Task Requires_a_scanned_reply_and_a_branch_head_action_before_creating_an_order(
        StudyRecommendation recommendation, StudyDiwanDecision decision, StudyDiwanStage finalStage, bool allowed)
    {
        await SeedAsync(recommendation);
        var (approvalKit, _) = Viewer(Head);
        await approvalKit.NotifyNextStepAsync(StudyEntityType.Response, 1);
        Assert.Equal(Clerk, (await _db.Notifications.SingleAsync()).UserId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Diwan(OtherBranch).Handle(
            new RegisterStudyDiwanDispatchCommand(1, "0004", Date, EncodedVersion), default));
        var sent = await Diwan(Clerk).Handle(new RegisterStudyDiwanDispatchCommand(1, "0004", Date, EncodedVersion), default);
        Assert.Equal((int)StudyDiwanStage.AwaitingReply, sent.DiwanStage);
        Assert.Equal("0004", sent.DiwanCase!.DispatchNumber);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Diwan(Clerk).Handle(
            new RegisterStudyDiwanReplyCommand(1, "0007", Date, decision, EncodedVersion, new PaperScan(false)), default));
        Assert.Empty(_db.StudyDiwanReplies);
        _db.ChangeTracker.Clear();

        var received = await Diwan(Clerk).Handle(new RegisterStudyDiwanReplyCommand(1, "0007", Date, decision,
            EncodedVersion, new PaperScan(true)), default);
        Assert.Equal((int)StudyDiwanStage.AwaitingBranchHead, received.DiwanStage);
        var reply = Assert.Single(received.DiwanCase!.Replies);
        Assert.Contains(_db.Notifications, note => note.UserId == Head);
        Assert.DoesNotContain(_db.Notifications, note => note.UserId == ProjectOffice);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateOrder(reply.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Diwan(Clerk).Handle(
            new ReviewStudyDiwanReplyCommand(1, StudyDiwanReviewAction.Confirm, null, EncodedVersion), default));

        var firstReplyId = reply.Id;
        if (allowed)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => Diwan(Head).Handle(
                new ReviewStudyDiwanReplyCommand(1, StudyDiwanReviewAction.ReturnForCorrection, "", EncodedVersion), default));
            var returned = await Diwan(Head).Handle(new ReviewStudyDiwanReplyCommand(1, StudyDiwanReviewAction.ReturnForCorrection,
                "تحقق من رقم الكتاب", EncodedVersion), default);
            Assert.Equal((int)StudyDiwanStage.CorrectionRequested, returned.DiwanStage);
            received = await Diwan(Clerk).Handle(new RegisterStudyDiwanReplyCommand(1, "0008", Date, decision,
                EncodedVersion, new PaperScan(true)), default);
            Assert.Equal(["0008", "0007"], received.DiwanCase!.Replies.Select(book => book.Number));
            reply = received.DiwanCase.Replies[0];
            var evidence = await Diwan(Head).Handle(new GetStudyDiwanReplyFileQuery(1, reply.Id), default);
            await using var stream = evidence.Content;
            Assert.Equal("application/pdf", evidence.ContentType);
            Assert.True(stream.Length > 0);
        }

        var reviewed = await Diwan(Head).Handle(new ReviewStudyDiwanReplyCommand(1, StudyDiwanReviewAction.Confirm, null, EncodedVersion), default);
        Assert.Equal((int)finalStage, reviewed.Chain!.DiwanStage);
        // الرفض المركزي يعيد الرد إلى القسم المتابع للتعديل؛ غيره يبقى الرد معتمداً
        Assert.Equal((int)(finalStage == StudyDiwanStage.Rejected ? StudyDocumentStatus.Rejected : StudyDocumentStatus.Approved), reviewed.Status);
        Assert.Equal(allowed, _db.Notifications.Any(note => note.UserId == ProjectOffice));
        if (!allowed)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateOrder(reply.Id));
            return;
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateOrder(firstReplyId));
        var order = await CreateOrder(reply.Id);
        Assert.Equal(reply.Id, order.DiwanReplyId);
        Assert.Equal("0008", order.DiwanReplyNumber);
        Assert.Equal((int)StudyDocumentStatus.Draft, order.Status);
        Assert.False(order.CanCreateProject);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateOrder(reply.Id));
    }

    [Fact]
    public async Task A_centrally_rejected_response_returns_for_amendment_and_is_sent_again_with_a_new_dispatch()
    {
        await SeedAsync(StudyRecommendation.Accept);
        await Diwan(Clerk).Handle(new RegisterStudyDiwanDispatchCommand(1, "0004", Date, EncodedVersion), default);
        var rejected = await Diwan(Clerk).Handle(new RegisterStudyDiwanReplyCommand(1, "0007", Date, StudyDiwanDecision.Rejected,
            EncodedVersion, new PaperScan(true)), default);
        var firstReply = Assert.Single(rejected.DiwanCase!.Replies);
        var returned = await Diwan(Head).Handle(new ReviewStudyDiwanReplyCommand(1, StudyDiwanReviewAction.Confirm, "تخفيض الكلفة", EncodedVersion), default);

        Assert.Equal((int)StudyDocumentStatus.Rejected, returned.Status);
        Assert.Equal((int)StudyDiwanStage.Rejected, returned.DiwanStage);
        Assert.False(returned.CanRegisterDiwanDispatch);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Diwan(Clerk).Handle(
            new RegisterStudyDiwanDispatchCommand(1, "0005", Date, EncodedVersion), default));

        // القسم المتابع عدّل الرد وأعاد اعتماده (التواقيع خارج هذا الاختبار)
        _db.ChangeTracker.Clear();
        var response = await _db.StudyAssignmentResponses.SingleAsync();
        response.Status = StudyDocumentStatus.Approved;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var again = await Diwan(Clerk).Handle(new RegisterStudyDiwanDispatchCommand(1, "0005", Date.AddDays(10), EncodedVersion), default);
        Assert.Equal((int)StudyDiwanStage.AwaitingReply, again.DiwanStage);
        Assert.Equal("0005", again.DiwanCase!.DispatchNumber);
        Assert.Null(again.DiwanCase.ReviewNote);
        Assert.Equal(firstReply.Id, Assert.Single(again.DiwanCase.Replies).Id);

        var approved = await Diwan(Clerk).Handle(new RegisterStudyDiwanReplyCommand(1, "0009", Date.AddDays(15), StudyDiwanDecision.Approved,
            EncodedVersion, new PaperScan(true)), default);
        Assert.Equal(["0009", "0007"], approved.DiwanCase!.Replies.Select(book => book.Number));
        await Diwan(Head).Handle(new ReviewStudyDiwanReplyCommand(1, StudyDiwanReviewAction.Confirm, null, EncodedVersion), default);
        var order = await CreateOrder(approved.DiwanCase.Replies[0].Id);
        Assert.Equal("0009", order.DiwanReplyNumber);
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
    [InlineData("Violation of UNIQUE KEY constraint 'IX_Other'.", "القيمة مستخدمة في سجل آخر")]
    public void A_concurrent_duplicate_names_the_field_and_the_value(string sql, string expected) =>
        Assert.StartsWith(expected, StudyRepositoryBase.DuplicateMessage(sql));

    private DiwanHandlers Diwan(int user)
    {
        var (kit, users) = Viewer(user);
        var responses = new StudyResponseService(_db);
        var support = new StudyResponseSupport(responses, users, new StudyApprovalService(_db), kit);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["FileStorage:Root"] = _filesRoot }).Build();
        var files = new FileStore(new FileStorageSettings(config));
        return new(new StudyDiwanSupport(responses, support, kit, _pusher), support, kit, files);
    }

    /// <summary>معالجات معاملة الديوان الأربعة بالاعتماديات نفسها، لاستدعائها في الاختبار كما يستدعيها MediatR</summary>
    private sealed class DiwanHandlers(StudyDiwanSupport diwan, StudyResponseSupport support, StudyKit kit, IFileStore files)
    {
        public Task<Application.DTOs.Response.StudyResponseDto> Handle(RegisterStudyDiwanDispatchCommand request, CancellationToken ct) =>
            new RegisterStudyDiwanDispatchCommandHandler(diwan).Handle(request, ct);

        public Task<Application.DTOs.Response.StudyResponseDto> Handle(RegisterStudyDiwanReplyCommand request, CancellationToken ct) =>
            new RegisterStudyDiwanReplyCommandHandler(files, diwan).Handle(request, ct);

        public Task<Application.DTOs.Response.StudyResponseDto> Handle(ReviewStudyDiwanReplyCommand request, CancellationToken ct) =>
            new ReviewStudyDiwanReplyCommandHandler(diwan).Handle(request, ct);

        public Task<Application.DTOs.Response.StudyAttachmentContentDto> Handle(GetStudyDiwanReplyFileQuery request, CancellationToken ct) =>
            new GetStudyDiwanReplyFileQueryHandler(support, kit, files).Handle(request, ct);
    }

    private Task<Application.DTOs.Response.StudyStartOrderDto> CreateOrder(int replyId)
    {
        var (kit, users) = Viewer(ProjectOffice);
        var orders = new StudyStartOrderService(_db);
        var support = new StudyStartOrderSupport(orders, new StudyLookupService(_db), new DepartmentService(_db), users, kit);
        var handler = new CreateStudyStartOrderCommandHandler(orders, new StudyResponseService(_db), new FixedNumber(), kit, support);
        return handler.Handle(new(new StudyStartOrderFormDto
        {
            DiwanReplyId = replyId, CompetentDepartmentId = 1, ProjectManagerUserId = ProjectOffice,
            OrderDate = Date.ToDateTime(TimeOnly.MinValue), StudyStartDate = Date.ToDateTime(TimeOnly.MinValue),
            StudyDeadline = Date.AddDays(7).ToDateTime(TimeOnly.MinValue)
        }), default);
    }

    private (StudyKit Kit, UserService Users) Viewer(int id)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, id.ToString())])) };
        var users = new UserService(_db, new HttpContextAccessor { HttpContext = context });
        return (new StudyKit(users, new UserPermissionService(_db), new StudyActivityLogService(_db),
            new StudyApprovalService(_db), new NotificationService(_db, _pusher), new StudyRequestService(_db),
            new StudyAssignmentService(_db), new StudyResponseService(_db), new StudyStartOrderService(_db), null!, null!, _mapper), users);
    }

    private async Task SeedAsync(StudyRecommendation recommendation)
    {
        _db.Branches.AddRange(new Branch { Id = 1, Name = "الدراسات" }, new Branch { Id = 2, Name = "فرع آخر" });
        _db.Departments.Add(new Department { Id = 1, BranchId = 1, Name = "قسم إدارة المشاريع" });
        _db.Permissions.AddRange(new Permission { Id = 1, Name = StudyPermissions.ViewResponses },
            new Permission { Id = 2, Name = StudyPermissions.ManageDiwan },
            new Permission { Id = 3, Name = StudyDiwanRules.HeadPermission },
            new Permission { Id = 4, Name = "CreateStudyStartOrder" });
        AddUser(Clerk, 1, 2);
        AddUser(Head, 1, 3);
        AddUser(ProjectOffice, 1, 4);
        AddUser(OtherBranch, 2, 2);
        _db.StudyRequests.Add(new StudyRequest { Id = 1, BranchId = 1, CreatedByUserId = Clerk, Status = StudyDocumentStatus.Approved });
        _db.StudyAssignments.Add(new StudyAssignment { Id = 1, RequestId = 1, FollowingDepartmentId = 1, CreatedByUserId = Clerk, Status = StudyDocumentStatus.Approved });
        _db.StudyAssignmentResponses.Add(new StudyAssignmentResponse
        {
            Id = 1, ResponseNumber = "RSP-26-0014", RowVersion = Version, AssignmentId = 1,
            CreatedByUserId = Clerk, Status = StudyDocumentStatus.Approved, Recommendation = recommendation
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    private void AddUser(int id, int branchId, int permission)
    {
        _db.Roles.Add(new Role { Id = id, Name = $"Role-{id}" });
        _db.Users.Add(new User { Id = id, RoleId = id, BranchId = branchId, FullName = $"User {id}", Email = $"user{id}@test.invalid", PasswordHash = "test" });
        _db.RolePermissions.AddRange(new RolePermission { RoleId = id, PermissionId = 1 },
            new RolePermission { RoleId = id, PermissionId = permission });
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

    private sealed class PaperScan(bool includeFile) : IUploadReader
    {
        public async Task<string?> CopyFileToAsync(Stream destination, CancellationToken ct)
        {
            if (!includeFile) return null;
            await destination.WriteAsync(Encoding.ASCII.GetBytes("%PDF-1.7\n"), ct);
            return "reply.pdf";
        }
    }
}
