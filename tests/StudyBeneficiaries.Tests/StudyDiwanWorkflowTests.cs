using System.Security.Claims;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.CentralStudies;
using Application.Features.CentralStudies.Approvals.Commands.Decide;
using Application.Features.CentralStudies.Attachments;
using Application.Features.CentralStudies.DiwanTransactions;
using Application.Features.CentralStudies.DiwanTransactions.Commands.Cancel;
using Application.Features.CentralStudies.DiwanTransactions.Commands.Create;
using Application.Features.CentralStudies.DiwanTransactions.Commands.Delete;
using Application.Features.CentralStudies.DiwanTransactions.Commands.Refer;
using Application.Features.CentralStudies.DiwanTransactions.Commands.RequestRevision;
using Application.Features.CentralStudies.DiwanTransactions.Commands.Return;
using Application.Features.CentralStudies.DiwanTransactions.Commands.Submit;
using Application.Features.CentralStudies.DiwanTransactions.Commands.Update;
using Application.Features.CentralStudies.Responses;
using Application.Features.CentralStudies.Responses.Commands.Submit;
using Application.Features.CentralStudies.StartOrders;
using Application.Features.CentralStudies.StartOrders.Commands.Create;
using Application.Helper.Profiles;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Entities.CentralStudiesBranch;
using Domain.Enums;
using Infrastructure.Files;
using Infrastructure.Persistence.Data;
using Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace StudyBeneficiaries.Tests;

/// <summary>
/// معاملة الديوان (DWN) بمسارها الجديد: الديوان يسجّل ويرسل، رئيس الفرع يحيل بموافقة أو رفض أو يعيد، مدير المشاريع يصدر أمر البدء
/// أو يختار تعديلاً تخطيطياً أو فنياً للرد، ثم صادر جديد على المعاملة نفسها.
/// </summary>
public sealed class StudyDiwanWorkflowTests : IDisposable
{
    private const int Clerk = 11;
    private const int Head = 12;
    private const int ProjectManager = 13;
    private const int OtherBranch = 14;
    private const int Admin = 15;
    private const int FollowingEditor = 16;
    private static readonly DateTime Date = new(2026, 11, 19);
    private readonly string _filesRoot = Path.Combine(Path.GetTempPath(), "ewms-diwan-test-" + Guid.NewGuid());
    private readonly DataContext _db = new(new DbContextOptionsBuilder<DataContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<CentralStudiesProfile>(), NullLoggerFactory.Instance).CreateMapper();
    private readonly SilentPusher _pusher = new();

    [Theory]
    [InlineData(StudyRecommendation.Modify, StudyDiwanDecision.Approved)]
    [InlineData(StudyRecommendation.NotAvailable, StudyDiwanDecision.Approved)]
    [InlineData(StudyRecommendation.Accept, StudyDiwanDecision.Rejected)]
    public async Task The_clerk_records_and_sends_and_the_branch_head_reads_the_decision_and_refers(
        StudyRecommendation recommendation, StudyDiwanDecision decision)
    {
        await SeedAsync(recommendation);
        var (headKit, _) = Viewer(Head);
        await headKit.NotifyNextStepAsync(StudyEntityType.Response, 1);
        Assert.Contains(_db.Notifications, n => n.UserId == Clerk);   // من يملك إنشاء المعاملة في الفرع

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Create(OtherBranch, "0004"));
        var created = await Create(Clerk, "0004");
        Assert.Equal($"DWN-{DateTime.Today.Year % 100:D2}-0001", created.TransactionNumber);
        Assert.Equal(((int)StudyDocumentStatus.Draft, (int)StudyDiwanStage.Registering, 1), (created.Status, created.Stage, created.RoundNumber));
        Assert.True(created.CanEdit && created.CanSubmit && created.CanDelete);
        Assert.Equal(created.Id, created.Chain!.DiwanId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(Clerk, "0005"));   // معاملة واحدة لكل رد

        await Assert.ThrowsAsync<InvalidOperationException>(() => Submit(Clerk, created.Id));          // لا رد مركزي بعد
        var updated = await Update(Clerk, created.Id, "0004", "0007");
        Assert.Equal("0007", updated.ReplyNumber);
        Assert.Null(updated.CentralDecision);                                                         // الموظف لا يحدد القرار
        await Assert.ThrowsAsync<InvalidOperationException>(() => Submit(Clerk, created.Id));          // لا صورة للكتاب
        AttachLetter(created.Id);
        var pending = await Submit(Clerk, created.Id);
        Assert.Equal(((int)StudyDocumentStatus.PendingApproval, (int)StudyDiwanStage.AtBranchHead), (pending.Status, pending.Stage));
        Assert.False(pending.CanEdit);
        var row = Assert.Single(pending.Approvals);
        Assert.Equal((int)StudySignerRole.BranchHead, row.SignerRole);
        Assert.Contains(_db.Notifications, n => n.UserId == Head && n.Type == NotificationType.StudyApprovalRequested);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateOrder(created.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Update(Clerk, created.Id, "0004", "0008"));   // ليست لدى الديوان

        // التوقيع العام لا يقرر في المعاملة: الإحالة من صفحتها
        await Assert.ThrowsAsync<InvalidOperationException>(() => Decide(Head, row.Id, StudyDecision.Approved));
        Assert.True((await Get(Head, created.Id)).CanRefer);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Refer(Clerk, created.Id, decision, "سبب"));   // لا يملك صفة رئيس الفرع
        var referred = await Refer(Head, created.Id, decision, decision == StudyDiwanDecision.Rejected ? "الكلفة أعلى من السقف" : null);

        var round = Assert.Single(referred.Rounds);
        Assert.Equal((1, "0004", "0007", (int)decision, Head), (round.RoundNumber, round.DispatchNumber, round.ReplyNumber, round.CentralDecision, round.ReferredByUserId));
        Assert.Equal((int)decision, referred.CentralDecision);
        var signed = Assert.Single(referred.Approvals);
        Assert.Equal((int)StudyDecision.Approved, signed.Decision);
        var transaction = await _db.StudyDiwanTransactions.Include(t => t.Response).AsNoTracking().SingleAsync();
        Assert.Equal(StudyDocumentStatus.Approved, transaction.Response.Status);   // لا يعود الرد تلقائياً إلى القسم المتابع
        Assert.False(_db.Notifications.Any(n => n.UserId == FollowingEditor));

        if (decision == StudyDiwanDecision.Rejected)
        {
            Assert.Equal((StudyDocumentStatus.Rejected, StudyDiwanStage.CentralRejected), (transaction.Status, transaction.Stage));
            Assert.Contains(_db.Notifications, n => n.UserId == ProjectManager && n.Title.Contains("بانتظار إحالتك"));
            var detail = await Get(ProjectManager, created.Id);
            Assert.True(detail.CanRequestRevision);
            Assert.False(detail.CanCreateStartOrder);
            Assert.Contains(detail.NextIssuers!, p => p.Id == ProjectManager);
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateOrder(created.Id));
            return;
        }

        Assert.Equal((StudyDocumentStatus.Approved, StudyDiwanStage.CentralApproved), (transaction.Status, transaction.Stage));
        var startAllowed = recommendation != StudyRecommendation.NotAvailable;
        Assert.Equal(startAllowed, _db.Notifications.Any(n => n.UserId == ProjectManager && n.Title.Contains("أمر البدء")));
        if (!startAllowed)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateOrder(created.Id));
            return;
        }
        var order = await CreateOrder(created.Id);
        Assert.Equal(created.Id, order.DiwanTransactionId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateOrder(created.Id));           // أمر واحد لكل رد
    }

    [Fact]
    public async Task A_planning_revision_keeps_the_following_signature_then_a_new_dispatch_goes_through_the_same_transaction()
    {
        var id = await CentralRejectionAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Revise(Clerk, id, StudyDiwanRevision.Planning));
        var revising = await Revise(ProjectManager, id, StudyDiwanRevision.Planning);
        Assert.Equal((int)StudyDiwanStage.PlanningRevision, revising.Stage);
        Assert.Equal((int)StudyDiwanRevision.Planning, revising.Rounds.Single().RevisionKind);
        Assert.False(revising.CanEdit);

        // الرد عاد إلى توقيع التخطيط وحده: يبقى توقيع القسم المتابع
        var response = await _db.StudyAssignmentResponses.Include(r => r.DiwanTransaction).ThenInclude(t => t!.Rounds).AsNoTracking().SingleAsync();
        Assert.Equal(StudyDocumentStatus.PendingApproval, response.Status);
        var rows = await _db.StudyApprovals.Where(a => a.ResponseId == 1).OrderBy(a => a.SortOrder).AsNoTracking().ToListAsync();
        Assert.Equal((StudyDecision.Approved, FollowingEditor), (rows[0].Decision, rows[0].SignerUserId!.Value));
        Assert.Equal((StudyDecision.Pending, (int?)null), (rows[1].Decision, rows[1].SignerUserId));
        var responseDto = await ResponseSupport(ProjectManager).DetailAsync(1, await KitOf(ProjectManager).ViewerAsync());
        Assert.True(responseDto.CanEditPlanningPart);
        Assert.Equal("أعد حساب المدة", responseDto.DiwanRevisionReason);
        Assert.Equal("الكلفة أعلى من السقف", responseDto.DiwanRejectionNote);
        Assert.Contains(_db.Notifications, n => n.UserId == ProjectManager && n.Type == NotificationType.StudyApprovalRequested);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Update(Clerk, id, "0010", "0011"));   // الرد قيد التعديل

        // مدير المشاريع يوقّع التخطيط وحده ← المعاملة جاهزة لصادر جديد ويُشعَر الديوان
        var before = _db.Notifications.Count(n => n.UserId == Clerk);
        await Decide(ProjectManager, rows[1].Id, StudyDecision.Approved);
        var ready = await Get(Clerk, id);
        Assert.Equal((int)StudyDiwanStage.ReadyToResend, ready.Stage);
        Assert.True(ready.CanEdit);
        Assert.False(ready.CanSubmit);
        Assert.True(_db.Notifications.Count(n => n.UserId == Clerk) > before);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Submit(Clerk, id));                  // الصادر الجديد أولاً

        await NewRoundAsync(id);
    }

    [Fact]
    public async Task A_technical_revision_returns_the_response_to_the_following_department_with_the_reason()
    {
        var id = await CentralRejectionAsync();
        var revising = await Revise(ProjectManager, id, StudyDiwanRevision.Technical, "عدّل كميات البنود");
        Assert.Equal((int)StudyDiwanStage.TechnicalRevision, revising.Stage);
        Assert.Contains(revising.NextIssuers!, p => p.Id == FollowingEditor);
        Assert.Contains(_db.Notifications, n => n.UserId == FollowingEditor && n.Message.Contains("عدّل كميات البنود"));
        Assert.Equal(StudyDocumentStatus.Rejected, (await _db.StudyAssignmentResponses.AsNoTracking().SingleAsync()).Status);

        // القسم المتابع يعدّل ويرسل ← يوقّع هو ثم التخطيط ← جاهزة لصادر جديد
        var submitted = await SubmitResponse(FollowingEditor);
        Assert.Equal((int)StudyDocumentStatus.PendingApproval, submitted.Status);
        Assert.Equal("عدّل كميات البنود", submitted.DiwanRevisionReason);
        var rows = await _db.StudyApprovals.Where(a => a.ResponseId == 1).OrderBy(a => a.SortOrder).AsNoTracking().ToListAsync();
        Assert.All(rows, r => Assert.Equal(StudyDecision.Pending, r.Decision));
        await Decide(FollowingEditor, rows[0].Id, StudyDecision.Approved);
        Assert.Equal((int)StudyDiwanStage.TechnicalRevision, (await Get(Clerk, id)).Stage);
        await Decide(ProjectManager, rows[1].Id, StudyDecision.Approved);
        Assert.Equal((int)StudyDiwanStage.ReadyToResend, (await Get(Clerk, id)).Stage);

        await NewRoundAsync(id);
    }

    [Fact]
    public async Task Rejecting_the_planning_signature_during_a_planning_revision_turns_it_into_a_technical_revision()
    {
        var id = await CentralRejectionAsync();
        await Revise(ProjectManager, id, StudyDiwanRevision.Planning);
        var planning = await _db.StudyApprovals.AsNoTracking().SingleAsync(a => a.ResponseId == 1 && a.SignerRole == StudySignerRole.PlanningAndProjects);
        await Decide(ProjectManager, planning.Id, StudyDecision.Rejected);
        Assert.Equal((int)StudyDiwanStage.TechnicalRevision, (await Get(Clerk, id)).Stage);
        Assert.Equal(StudyDocumentStatus.Rejected, (await _db.StudyAssignmentResponses.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task The_branch_head_can_return_the_transaction_to_the_diwan_for_correction()
    {
        await SeedAsync(StudyRecommendation.Accept);
        var created = await Create(Clerk, "0004");
        await Update(Clerk, created.Id, "0004", "0007");
        AttachLetter(created.Id);
        await Submit(Clerk, created.Id);

        var returned = await Return(Head, created.Id, "صورة الكتاب غير واضحة");
        Assert.Equal(((int)StudyDocumentStatus.Rejected, (int)StudyDiwanStage.ReturnedToDiwan), (returned.Status, returned.Stage));
        Assert.Empty(returned.Rounds);                                         // الإعادة ليست جولة: الصادر نفسه يُصحَّح
        Assert.Null(returned.CentralDecision);
        Assert.Equal((int)StudyDecision.Rejected, returned.Approvals.Single().Decision);
        Assert.Contains(_db.Notifications, n => n.UserId == Clerk && n.Title.Contains("للتصحيح"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Refer(Head, created.Id, StudyDiwanDecision.Approved, null));

        var detail = await Get(Clerk, created.Id);
        Assert.True(detail.CanEdit && detail.CanSubmit);
        Assert.False(detail.CanDelete);                                        // ليست مسودة
        var again = await Submit(Clerk, created.Id);                           // تصحيح المرفق فقط يكفي
        Assert.Equal((int)StudyDiwanStage.AtBranchHead, again.Stage);
        Assert.Equal((int)StudyDecision.Pending, again.Approvals.Single().Decision);
        var approved = await Refer(Head, created.Id, StudyDiwanDecision.Approved, null);
        Assert.Equal(1, approved.Rounds.Single().RoundNumber);
    }

    [Fact]
    public async Task Attachments_are_locked_outside_the_diwan_and_past_round_letters_stay()
    {
        var id = await CentralRejectionAsync();
        var (kit, _) = Viewer(Clerk);
        var viewer = await kit.ViewerAsync();
        var owner = await kit.ResolveOwnerAsync(StudyEntityType.DiwanTransaction, id);
        Assert.True(owner.Closed);
        Assert.NotNull(owner.ClosedReason);                                    // ليست ملغاة: مقفلة لأنها خارج الديوان
        Assert.False(StudyAttachmentSupport.CanEdit(viewer, owner));

        await Revise(ProjectManager, id, StudyDiwanRevision.Planning);
        var planning = await _db.StudyApprovals.AsNoTracking().SingleAsync(a => a.ResponseId == 1 && a.SignerRole == StudySignerRole.PlanningAndProjects);
        await Decide(ProjectManager, planning.Id, StudyDecision.Approved);
        _db.ChangeTracker.Clear();
        owner = await KitOf(Clerk).ResolveOwnerAsync(StudyEntityType.DiwanTransaction, id);
        Assert.False(owner.Closed);
        var oldLetter = await _db.StudyAttachments.AsNoTracking().SingleAsync();
        Assert.False(StudyAttachmentSupport.CanDelete(viewer, owner, oldLetter));   // كتاب الجولة الأولى للأرشيف
        AttachLetter(id);
        var newLetter = await _db.StudyAttachments.AsNoTracking().OrderBy(a => a.Id).LastAsync();
        Assert.True(StudyAttachmentSupport.CanDelete(viewer, owner, newLetter));
    }

    [Fact]
    public async Task A_transaction_with_past_rounds_is_cancelled_not_deleted()
    {
        var id = await CentralRejectionAsync();
        await Revise(ProjectManager, id, StudyDiwanRevision.Planning);
        var planning = await _db.StudyApprovals.AsNoTracking().SingleAsync(a => a.ResponseId == 1 && a.SignerRole == StudySignerRole.PlanningAndProjects);
        await Decide(ProjectManager, planning.Id, StudyDecision.Approved);
        var draft = await Update(Clerk, id, "0010", "0011");
        Assert.Equal(((int)StudyDocumentStatus.Draft, 2), (draft.Status, draft.RoundNumber));
        Assert.False(draft.CanDelete);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new DeleteStudyDiwanTransactionCommandHandler(new StudyDiwanTransactionService(_db), KitOf(Admin), Support(Admin)).Handle(new(id), default));
        var cancelled = await new CancelStudyDiwanTransactionCommandHandler(new StudyDiwanTransactionService(_db), KitOf(Admin), Support(Admin))
            .Handle(new(id, "أوقفت الجهة المشروع"), default);
        Assert.Equal(((int)StudyDocumentStatus.Cancelled, (int)StudyDiwanStage.Cancelled), (cancelled.Status, cancelled.Stage));
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

    // ════════════════════ مراحل مشتركة ════════════════════

    /// <summary>معاملة أرسلها الديوان وأحالها رئيس الفرع برفض مركزي</summary>
    private async Task<int> CentralRejectionAsync()
    {
        await SeedAsync(StudyRecommendation.Accept);
        var created = await Create(Clerk, "0004");
        await Update(Clerk, created.Id, "0004", "0007");
        AttachLetter(created.Id);
        await Submit(Clerk, created.Id);
        await Refer(Head, created.Id, StudyDiwanDecision.Rejected, "الكلفة أعلى من السقف");
        return created.Id;
    }

    /// <summary>بعد اعتماد الرد المعدّل: صادر جديد ورد جديد وصورته ← رئيس الفرع ← أمر البدء</summary>
    private async Task NewRoundAsync(int id)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Update(Clerk, id, "0004", "0011"));   // صادر الجولة السابقة
        var second = await Update(Clerk, id, "0010", "0007");
        Assert.Equal((2, (int)StudyDiwanStage.Registering), (second.RoundNumber, second.Stage));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Submit(Clerk, id));                   // رد الجولة السابقة
        await Update(Clerk, id, "0010", "0011");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Submit(Clerk, id));                   // لا صورة جديدة
        AttachLetter(id);
        var pending = await Submit(Clerk, id);
        Assert.Equal((int)StudyDiwanStage.AtBranchHead, pending.Stage);
        var approved = await Refer(Head, id, StudyDiwanDecision.Approved, null);
        Assert.Equal(new[] { (1, 2), (2, 1) }, approved.Rounds.Select(r => (r.RoundNumber, r.CentralDecision)));
        Assert.Equal(1, await _db.StudyDiwanTransactions.CountAsync());
        var order = await CreateOrder(id);
        Assert.Equal(id, order.DiwanTransactionId);
        Assert.Equal("0011", (await _db.StudyDiwanTransactions.SingleAsync()).ReplyNumber);
    }

    // ════════════════════ استدعاء المعالجات كما يستدعيها MediatR ════════════════════

    private Task<StudyDiwanTransactionDto> Create(int user, string dispatch)
    {
        var kit = KitOf(user);
        return new CreateStudyDiwanTransactionCommandHandler(new StudyDiwanTransactionService(_db), new StudyResponseService(_db), new FixedNumber(), kit, Support(kit))
            .Handle(new(new StudyDiwanTransactionFormDto { ResponseId = 1, DispatchNumber = dispatch, DispatchDate = Date }), default);
    }

    private async Task<StudyDiwanTransactionDto> Update(int user, int id, string dispatch, string reply)
    {
        var kit = KitOf(user);
        var current = await _db.StudyDiwanTransactions.AsNoTracking().SingleAsync(t => t.Id == id);
        try
        {
            return await new UpdateStudyDiwanTransactionCommandHandler(new StudyDiwanTransactionService(_db), kit, Support(kit)).Handle(new(id, new StudyDiwanTransactionFormDto
            {
                DispatchNumber = dispatch, DispatchDate = Date, ReplyNumber = reply, ReplyDate = Date.AddDays(5), RowVersion = current.RowVersion
            }), default);
        }
        finally { _db.ChangeTracker.Clear(); }
    }

    private async Task<StudyDiwanTransactionDto> Submit(int user, int id)
    {
        var kit = KitOf(user);
        try
        {
            return await new SubmitStudyDiwanTransactionCommandHandler(new StudyDiwanTransactionService(_db), new StudyAttachmentService(_db), kit, Support(kit))
                .Handle(new(id), default);
        }
        finally { _db.ChangeTracker.Clear(); }
    }

    private async Task<StudyDiwanTransactionDto> Refer(int user, int id, StudyDiwanDecision decision, string? note)
    {
        var kit = KitOf(user);
        try
        {
            return await new ReferStudyDiwanTransactionCommandHandler(new StudyDiwanTransactionService(_db), kit, Support(kit))
                .Handle(new(id, new StudyDiwanReferFormDto { CentralDecision = (int)decision, Note = note }), default);
        }
        finally { _db.ChangeTracker.Clear(); }
    }

    private async Task<StudyDiwanTransactionDto> Return(int user, int id, string reason)
    {
        var kit = KitOf(user);
        try
        {
            return await new ReturnStudyDiwanTransactionCommandHandler(new StudyDiwanTransactionService(_db), kit, Support(kit))
                .Handle(new(id, new StudyDiwanReturnFormDto { Reason = reason }), default);
        }
        finally { _db.ChangeTracker.Clear(); }
    }

    private async Task<StudyDiwanTransactionDto> Revise(int user, int id, StudyDiwanRevision kind, string reason = "أعد حساب المدة")
    {
        var kit = KitOf(user);
        try
        {
            // الصلاحية على المسار (HandleStudyDiwanRejection)؛ هنا يكفي حدّ الفرع، فيُرفض من لا يملكها كما يرفضها المسار
            if (!(await kit.ViewerAsync()).Has(StudyPermissions.HandleDiwanRejection)) throw new UnauthorizedAccessException();
            return await new RequestStudyDiwanRevisionCommandHandler(new StudyDiwanTransactionService(_db), new StudyApprovalService(_db), kit, Support(kit))
                .Handle(new(id, new StudyDiwanRevisionFormDto { Kind = (int)kind, Reason = reason }), default);
        }
        finally { _db.ChangeTracker.Clear(); }
    }

    private async Task<StudyDiwanTransactionDto> Get(int user, int id)
    {
        var kit = KitOf(user);
        try { return await Support(kit).DetailAsync(id, await kit.ViewerAsync()); }
        finally { _db.ChangeTracker.Clear(); }
    }

    private async Task Decide(int user, int approvalId, StudyDecision decision)
    {
        var kit = KitOf(user);
        try
        {
            await new DecideStudyApprovalCommandHandler(new StudyApprovalService(_db), new UserSignatureService(_db), kit)
                .Handle(new(approvalId, new StudyDecisionDto { Decision = (int)decision, Note = decision == StudyDecision.Rejected ? "سبب" : null }), default);
        }
        finally { _db.ChangeTracker.Clear(); }
    }

    private async Task<StudyResponseDto> SubmitResponse(int user)
    {
        var kit = KitOf(user);
        try
        {
            return await new SubmitStudyResponseCommandHandler(new StudyResponseService(_db), kit, ResponseSupport(user, kit)).Handle(new(1), default);
        }
        finally { _db.ChangeTracker.Clear(); }
    }

    private async Task<StudyStartOrderDto> CreateOrder(int transactionId)
    {
        var (kit, users) = Viewer(ProjectManager);
        var orders = new StudyStartOrderService(_db);
        var support = new StudyStartOrderSupport(orders, new StudyLookupService(_db), new DepartmentService(_db), users, kit);
        var handler = new CreateStudyStartOrderCommandHandler(orders, new StudyDiwanTransactionService(_db), new FixedNumber(), kit, support);
        try
        {
            return await handler.Handle(new(new StudyStartOrderFormDto
            {
                DiwanTransactionId = transactionId, CompetentDepartmentId = 1, ProjectManagerUserId = ProjectManager,
                OrderDate = Date, StudyStartDate = Date, StudyDeadline = Date.AddDays(7)
            }), default);
        }
        finally { _db.ChangeTracker.Clear(); }
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

    private StudyDiwanTransactionSupport Support(StudyKit kit) =>
        new(new StudyDiwanTransactionService(_db), new StudyApprovalService(_db), new UserSignatureService(_db), kit);

    private StudyDiwanTransactionSupport Support(int user) => Support(KitOf(user));

    private StudyResponseSupport ResponseSupport(int user, StudyKit? kit = null)
    {
        var (own, users) = Viewer(user);
        return new StudyResponseSupport(new StudyResponseService(_db), users, new StudyApprovalService(_db), kit ?? own);
    }

    private StudyKit KitOf(int user) => Viewer(user).Kit;

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
        string[] names = [StudyPermissions.ViewDiwan, StudyPermissions.CreateDiwan, StudyPermissions.EditDiwan,     // 1 2 3
            StudyPermissions.SignAsBranchHead, "CreateStudyStartOrder", StudyPermissions.EditResponse,                // 4 5 6
            StudyPermissions.HandleDiwanRejection, StudyPermissions.EditResponsePlanning, StudyPermissions.SignAsPlanning,   // 7 8 9
            "SignStudyAsFollowingDepartment", StudyPermissions.DeleteDiwan, StudyPermissions.CancelDocuments];        // 10 11 12
        _db.Permissions.AddRange(names.Select((name, i) => new Permission { Id = i + 1, Name = name }));
        AddUser(Clerk, 1, null, [1, 2, 3, 11]);
        AddUser(Head, 1, null, [1, 4]);
        AddUser(ProjectManager, 1, null, [1, 5, 7, 8, 9]);
        AddUser(OtherBranch, 2, null, [1, 2, 3]);
        AddUser(FollowingEditor, 1, 1, [6, 10]);
        AddUser(Admin, null, null, [1, 2, 3, 11, 12], "SuperAdmin");
        _db.StudyBranches.Add(new StudyBranch { Id = 1, BranchId = 1, Code = 0, Name = "المركزية", Track = StudyTrack.Full, IsActive = true });
        _db.StudyRequests.Add(new StudyRequest { Id = 1, BranchId = 1, StudyBranchId = 1, CreatedByUserId = Clerk, Status = StudyDocumentStatus.Approved, MainBeneficiary = "جهة" });
        _db.StudyAssignments.Add(new StudyAssignment { Id = 1, RequestId = 1, FollowingDepartmentId = 1, CreatedByUserId = Clerk, Status = StudyDocumentStatus.Approved, AssignmentNumber = "IPA-26-0001" });
        _db.StudyAssignmentResponses.Add(new StudyAssignmentResponse
        {
            Id = 1, ResponseNumber = "RSP-26-0014", AssignmentId = 1,
            CreatedByUserId = FollowingEditor, Status = StudyDocumentStatus.Approved, Recommendation = recommendation
        });
        // الرد المعتمد وقّعه القسم المتابع ثم التخطيط
        _db.StudyApprovals.AddRange(
            new StudyApproval { ResponseId = 1, SignerRole = StudySignerRole.FollowingDepartment, SortOrder = 1, Decision = StudyDecision.Approved,
                SignerUserId = FollowingEditor, DecidedAt = Date },
            new StudyApproval { ResponseId = 1, SignerRole = StudySignerRole.PlanningAndProjects, SortOrder = 2, Decision = StudyDecision.Approved,
                SignerUserId = ProjectManager, DecidedAt = Date });
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
