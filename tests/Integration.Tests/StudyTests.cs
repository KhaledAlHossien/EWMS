using System.Net;
using System.Text.Json;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Studies;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence.Data;
using Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests;

/// <summary>
/// الدراسات والمكاتب الفنية على القاعدة الحقيقية: المسار الكامل (إعداد ← مراجعة ← اعتماد ← إرسال ← قرار ← جولة ثانية)،
/// التسلسل، المراجع غير المُعدّ، حدّ الرؤية، الإصدار المعتمد في الإضبارة، التنبيه مرة لكل موعد، والتقرير.
/// </summary>
[Collection(ApiCollection.Name)]
public class StudyTests(ApiFixture api)
{
    private const string Base = "/api/Studies";

    private sealed record Org(ApiClient Head, int HeadId, ApiClient Engineer, int EngineerId, ApiClient Reviewer, ApiClient SelfReviewer, int SelfReviewerId,
        int ArchitecturalTypeId, int StructuralTypeId);

    private static readonly object Gate = new();
    private static Task<Org>? _shared;

    private Task<Org> OrgAsync() { lock (Gate) return _shared ??= CreateOrgAsync(); }

    private async Task<(ApiClient Client, int Id)> UserAsync(string label, int roleId)
    {
        var email = $"{label}-{Guid.NewGuid():N}"[..22] + "@test.sy";
        var id = await api.CreateUserAsync("دراسات " + label, email, roleId, api.BranchId, api.DepartmentId);
        await using var scope = api.Services.CreateAsyncScope();
        var login = (await scope.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(new LoginRequest { Email = email, Password = ApiFixture.Password })).Response!;
        return (api.WithToken(login.Token, id), id);
    }

    private async Task<Org> CreateOrgAsync()
    {
        var all = api.AllPermissions;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var headRole = await api.CreateRoleAsync("رئيس دراسات " + suffix,
            ["ViewProjects", "ViewBranchProjects", "ViewStudies", "CreateStudyOrder", "EditStudyOrder", "RecordStudySubmission", "ViewDeliverableTypes", "ViewNotifications"], all);
        var engineerRole = await api.CreateRoleAsync("مهندس دراسات " + suffix,
            ["ViewProjects", "ViewStudies", "PrepareStudyDeliverable", "UploadProjectDocument", "ViewProjectDocuments", "ViewNotifications"], all);
        var reviewerRole = await api.CreateRoleAsync("مراجع دراسات " + suffix,
            ["ViewProjects", "ViewBranchProjects", "ViewStudies", "ReviewStudyDeliverable", "ViewNotifications"], all);
        var selfRole = await api.CreateRoleAsync("مهندس ومراجع " + suffix,
            ["ViewProjects", "ViewStudies", "PrepareStudyDeliverable", "ReviewStudyDeliverable", "UploadProjectDocument", "ViewProjectDocuments"], all);

        var head = await UserAsync("head", headRole);
        var engineer = await UserAsync("eng", engineerRole);
        var reviewer = await UserAsync("rev", reviewerRole);
        var self = await UserAsync("self", selfRole);

        async Task<int> Type(string name, int kind) => (await api.Admin.Post<DeliverableTypeResponseDto>("/api/DeliverableTypes/Create",
            new { branchId = api.BranchId, name = name + " " + Guid.NewGuid().ToString("N")[..5], documentKind = kind, isActive = true, sortOrder = 1 })).Ok().Id;
        var architectural = await Type("معماري", 4);
        var structural = await Type("إنشائي", 5);

        return new Org(head.Client, head.Id, engineer.Client, engineer.Id, reviewer.Client, self.Client, self.Id, architectural, structural);
    }

    /// <summary>مشروع جديد بمُعِدّ الجدول منشئاً، والمهندسان مشاركان فيه</summary>
    private async Task<int> NewProjectAsync(Org org)
    {
        var project = (await api.Preparer.Post<ProjectResponseDto>("/api/Projects/Create", new ProjectRequestDto
        {
            Name = $"مشروع دراسة {Guid.NewGuid():N}"[..30], Beneficiary = "جهة اختبار", DepartmentId = api.DepartmentId
        })).Ok();
        foreach (var userId in new[] { org.EngineerId, org.SelfReviewerId })
            (await api.Preparer.Post<JsonElement>($"/api/Projects/Members/{project.Id}", new { userId, title = "مهندس دراسة" })).Ok();
        return project.Id;
    }

    /// <summary>أمر مفتوح بمخرجين بالتسلسل (معماري ثم إنشائي) مسندين إلى المهندس</summary>
    private async Task<StudyDetailDto> NewStudyAsync(Org org, int projectId, int mode = 1)
    {
        var study = (await org.Head.Post<StudyDetailDto>($"{Base}/Create", new { projectId, title = "دراسة المدرسة", mode })).Ok();
        study = (await org.Head.Post<StudyDetailDto>($"{Base}/Deliverables/{study.Id}", new { typeId = org.ArchitecturalTypeId, assigneeUserId = org.EngineerId })).Ok();
        study = (await org.Head.Post<StudyDetailDto>($"{Base}/Deliverables/{study.Id}", new { typeId = org.StructuralTypeId, assigneeUserId = org.EngineerId })).Ok();
        return study;
    }

    /// <summary>رفع حقيقي مجزّأ لملف صغير؛ يعيد الوثيقة والإصدار (documentId يضيف إصداراً لوثيقة قائمة)</summary>
    private static async Task<(int DocumentId, int VersionId)> UploadAsync(ApiClient client, int projectId, string title, int? documentId = null)
    {
        var content = System.Text.Encoding.UTF8.GetBytes($"{title} — {Guid.NewGuid()}");
        var started = (await client.Post<ProjectUploadResponseDto>("/api/ProjectUploads/Start", new StartProjectUploadRequestDto
        {
            ProjectId = projectId, DocumentId = documentId, Title = title, Kind = 4, FileName = "study.txt", Size = content.Length
        })).Ok();
        (await client.PutBytes<ProjectUploadResponseDto>($"/api/ProjectUploads/{started.Id}/Chunk?offset=0", content)).Ok();
        (await client.Post<ProjectUploadResponseDto>($"/api/ProjectUploads/{started.Id}/Complete")).Ok();

        for (var attempt = 0; attempt < 40; attempt++)
        {
            await Task.Delay(500);
            var state = (await client.Get<ProjectUploadResponseDto>($"/api/ProjectUploads/{started.Id}")).Ok();
            if (state.Status == 3 && state.VersionId is int version)
            {
                var found = (await client.Get<PagedResultDto<ProjectDocumentResponseDto>>($"/api/ProjectDocuments/Search?projectId={projectId}&search={Uri.EscapeDataString(title)}")).Ok();
                return (documentId ?? found.Items.Single(d => d.Title == title).Id, version);
            }
            if (state.Status == 4) throw new Xunit.Sdk.XunitException($"فشل إنهاء الرفع: {state.Error}");
        }
        throw new Xunit.Sdk.XunitException("لم ينتهِ الرفع في المهلة");
    }

    private static StudyDeliverableDto Of(StudyDetailDto study, int typeId) => study.Deliverables.Single(d => d.TypeId == typeId);

    /// <summary>المهندس يرفع ملف المخرج ويربطه ويرفعه للمراجعة</summary>
    private static async Task<StudyDetailDto> PrepareAndSubmitAsync(ApiClient engineer, int projectId, StudyDetailDto study, int typeId)
    {
        var d = Of(study, typeId);
        var (document, _) = await UploadAsync(engineer, projectId, d.DocumentId == null ? $"مخرج {d.TypeName} {Guid.NewGuid():N}" : "إصدار جديد", d.DocumentId);
        if (d.DocumentId == null)
            study = (await engineer.Put<StudyDetailDto>($"{Base}/Deliverable/{d.Id}/Document", new { documentId = document })).Ok();
        d = Of(study, typeId);
        return (await engineer.Post<StudyDetailDto>($"{Base}/Deliverable/{d.Id}/Submit", new { rowVersion = d.RowVersion })).Ok();
    }

    // ───────── أنواع المخرجات ─────────

    [Fact]
    public async Task Deliverable_types_are_read_by_staff_but_managed_only_with_their_own_permissions()
    {
        var org = await OrgAsync();
        var types = (await org.Head.Get<List<DeliverableTypeResponseDto>>("/api/DeliverableTypes/GetAll")).Ok();
        Assert.Contains(types, t => t.Id == org.ArchitecturalTypeId);

        Assert.Equal(HttpStatusCode.Forbidden, (await org.Head.Post<DeliverableTypeResponseDto>("/api/DeliverableTypes/Create", new { name = "نوع", documentKind = 99 })).Status);
        var duplicate = types.First(t => t.Id == org.ArchitecturalTypeId);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Admin.Post<DeliverableTypeResponseDto>("/api/DeliverableTypes/Create",
            new { branchId = api.BranchId, name = duplicate.Name, documentKind = 4 })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Admin.Post<DeliverableTypeResponseDto>("/api/DeliverableTypes/Create",
            new { branchId = api.BranchId, name = "نوع وثيقة غير معروف", documentKind = 1234 })).Status);
    }

    [Fact]
    public async Task A_type_with_deliverables_cannot_be_deleted()
    {
        var org = await OrgAsync();
        var project = await NewProjectAsync(org);
        await NewStudyAsync(org, project);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Admin.Delete($"/api/DeliverableTypes/Delete/{org.ArchitecturalTypeId}")).Status);
    }

    // ───────── المسار الكامل ─────────

    [Fact]
    public async Task The_full_path_runs_prepare_review_approve_send_notes_new_round_and_center_approval()
    {
        var org = await OrgAsync();
        var project = await NewProjectAsync(org);
        var study = await NewStudyAsync(org, project);
        Assert.Matches(@"^STD-\d{4}-\d{5}$", study.Number);
        Assert.Equal((int)StudyStatus.Open, study.Status);

        // بالتسلسل: الإنشائي ينتظر المعماري
        var structural = Of(study, org.StructuralTypeId);
        Assert.False(structural.CanPrepare);
        Assert.Equal(Of(study, org.ArchitecturalTypeId).TypeName, structural.BlockedByName);
        var blocked = await org.Engineer.Put<StudyDetailDto>($"{Base}/Deliverable/{structural.Id}/Document", new { documentId = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, blocked.Status);

        // المعماري: إعداد ← رفع للمراجعة
        study = await PrepareAndSubmitAsync(org.Engineer, project, study, org.ArchitecturalTypeId);
        var architectural = Of(study, org.ArchitecturalTypeId);
        Assert.Equal((int)DeliverableState.InReview, architectural.State);
        Assert.NotNull(architectural.SubmittedVersionId);
        Assert.False(architectural.CanPrepare);   // مجمَّد

        // المُعدّ لا يملك صلاحية المراجعة أصلاً، والإعادة بلا سبب مرفوضة
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Engineer.Post<StudyDetailDto>($"{Base}/Deliverable/{architectural.Id}/Review", new { approve = true })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Reviewer.Post<StudyDetailDto>($"{Base}/Deliverable/{architectural.Id}/Review", new { approve = false })).Status);

        // الإعادة بسبب، ثم إصدار جديد، ثم رفع، ثم اعتماد
        var returned = (await org.Reviewer.Post<StudyDetailDto>($"{Base}/Deliverable/{architectural.Id}/Review", new { approve = false, note = "عدّل المقياس" })).Ok();
        Assert.Equal((int)DeliverableState.Returned, Of(returned, org.ArchitecturalTypeId).State);
        Assert.Equal("عدّل المقياس", Of(returned, org.ArchitecturalTypeId).ReturnNote);
        var sameVersion = await org.Engineer.Post<StudyDetailDto>($"{Base}/Deliverable/{architectural.Id}/Submit", new { rowVersion = Of(returned, org.ArchitecturalTypeId).RowVersion });
        Assert.Equal(HttpStatusCode.BadRequest, sameVersion.Status);   // يلزم إصدار أحدث

        study = await PrepareAndSubmitAsync(org.Engineer, project, returned, org.ArchitecturalTypeId);
        architectural = Of(study, org.ArchitecturalTypeId);
        Assert.Equal(2, architectural.SubmittedVersionNumber);
        study = (await org.Reviewer.Post<StudyDetailDto>($"{Base}/Deliverable/{architectural.Id}/Review", new { approve = true })).Ok();
        architectural = Of(study, org.ArchitecturalTypeId);
        Assert.Equal((int)DeliverableState.Approved, architectural.State);
        Assert.Equal(architectural.SubmittedVersionId, architectural.ApprovedVersionId);

        // الإصدار المعتمد صار معتمداً في إضبارة المشروع أيضاً
        var document = (await api.Preparer.Get<ProjectDocumentResponseDto>($"/api/ProjectDocuments/Get/{architectural.DocumentId}")).Ok();
        Assert.True(document.Versions!.Single(v => v.Id == architectural.ApprovedVersionId).IsApproved);
        Assert.False(document.Versions!.Single(v => v.VersionNumber == 1).IsApproved);

        // الإنشائي يبدأ الآن، ولا إرسال قبل اعتماده
        study = (await org.Head.Get<StudyDetailDto>($"{Base}/Get/{study.Id}")).Ok();
        Assert.False(study.CanSend);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Head.Post<StudyDetailDto>($"{Base}/Send/{study.Id}", new { })).Status);
        study = await PrepareAndSubmitAsync(org.Engineer, project, study, org.StructuralTypeId);
        study = (await org.Reviewer.Post<StudyDetailDto>($"{Base}/Deliverable/{Of(study, org.StructuralTypeId).Id}/Review", new { approve = true })).Ok();
        Assert.Equal(100, study.ApprovedPercent);

        // الإرسال إلى المركزية ثم الملاحظات ثم جولة جديدة
        study = (await org.Head.Post<StudyDetailDto>($"{Base}/Send/{study.Id}", new { note = "أُرسلت باليد" })).Ok();
        Assert.Equal((int)StudyStatus.SentToCenter, study.Status);
        var submission = Assert.Single(study.Submissions);
        Assert.Equal(1, submission.Round);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Head.Post<StudyDetailDto>($"{Base}/Send/{study.Id}", new { })).Status);       // جولة واحدة معلّقة
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Head.Post<StudyDetailDto>($"{Base}/Decide/{submission.Id}", new { decision = 3 })).Status);   // الملاحظات تشترط نصاً
        study = (await org.Head.Post<StudyDetailDto>($"{Base}/Decide/{submission.Id}", new { decision = 3, note = "راجعوا الكميات" })).Ok();
        Assert.Equal((int)StudyStatus.ReturnedByCenter, study.Status);
        Assert.True(study.CanReopen);

        study = (await org.Head.Post<StudyDetailDto>($"{Base}/Reopen/{study.Id}", new { rowVersion = study.RowVersion })).Ok();
        Assert.Equal((int)StudyStatus.Open, study.Status);
        var structuralDone = Of(study, org.StructuralTypeId);
        Assert.True(structuralDone.CanSendBack);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Head.Post<StudyDetailDto>($"{Base}/Deliverable/{structuralDone.Id}/SendBack", new { note = "" })).Status);
        study = (await org.Head.Post<StudyDetailDto>($"{Base}/Deliverable/{structuralDone.Id}/SendBack", new { note = "حدّثوا الكميات" })).Ok();
        Assert.Equal((int)DeliverableState.Returned, Of(study, org.StructuralTypeId).State);

        study = await PrepareAndSubmitAsync(org.Engineer, project, study, org.StructuralTypeId);
        study = (await org.Reviewer.Post<StudyDetailDto>($"{Base}/Deliverable/{Of(study, org.StructuralTypeId).Id}/Review", new { approve = true })).Ok();
        study = (await org.Head.Post<StudyDetailDto>($"{Base}/Send/{study.Id}", new { })).Ok();
        Assert.Equal(2, study.Submissions.Max(s => s.Round));
        var second = study.Submissions.Single(s => s.Round == 2);
        study = (await org.Head.Post<StudyDetailDto>($"{Base}/Decide/{second.Id}", new { decision = 2 })).Ok();
        Assert.Equal((int)StudyStatus.ApprovedByCenter, study.Status);
        Assert.False(study.CanEdit);

        // سجل التدقيق يحمل العمليات (في إضبارة المشروع)
        var audit = (await api.Preparer.Get<PagedResultDto<AuditLogResponseDto>>($"/api/Projects/Audit/{project}?pageSize=100")).Ok();
        Assert.Contains(audit.Items, a => a.EntityType == "StudyOrder" && a.Action == "created");
        Assert.Contains(audit.Items, a => a.EntityType == "StudyDeliverable" && a.Action == "approved");
        Assert.Contains(audit.Items, a => a.EntityType == "StudySubmission" && a.Action == "decided");
    }

    [Fact]
    public async Task In_a_parallel_order_every_deliverable_can_be_prepared_at_once()
    {
        var org = await OrgAsync();
        var project = await NewProjectAsync(org);
        var study = await NewStudyAsync(org, project, mode: 2);
        study = (await org.Engineer.Get<StudyDetailDto>($"{Base}/Get/{study.Id}")).Ok();
        Assert.All(study.Deliverables, d => Assert.True(d.CanPrepare));
        Assert.All(study.Deliverables, d => Assert.Null(d.BlockedByName));
    }

    [Fact]
    public async Task The_one_who_prepared_a_deliverable_never_reviews_it_even_with_the_review_permission()
    {
        var org = await OrgAsync();
        var project = await NewProjectAsync(org);
        var study = (await org.Head.Post<StudyDetailDto>($"{Base}/Create", new { projectId = project, title = "دراسة", mode = 2 })).Ok();
        study = (await org.Head.Post<StudyDetailDto>($"{Base}/Deliverables/{study.Id}", new { typeId = org.ArchitecturalTypeId, assigneeUserId = org.SelfReviewerId })).Ok();

        study = await PrepareAndSubmitAsync(org.SelfReviewer, project, study, org.ArchitecturalTypeId);
        var d = Of(study, org.ArchitecturalTypeId);
        Assert.False(d.CanReview);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.SelfReviewer.Post<StudyDetailDto>($"{Base}/Deliverable/{d.Id}/Review", new { approve = true })).Status);
        Assert.True(Of((await org.Reviewer.Get<StudyDetailDto>($"{Base}/Get/{study.Id}")).Ok(), org.ArchitecturalTypeId).CanReview);
    }

    // ───────── الحدود والصلاحيات ─────────

    [Fact]
    public async Task A_study_follows_the_project_boundary_and_each_operation_its_own_permission()
    {
        var org = await OrgAsync();
        var project = await NewProjectAsync(org);
        var study = await NewStudyAsync(org, project);

        // خارج حدّ المشروع (فرع آخر) لا يرى الأمر
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Outsider.Get<StudyDetailDto>($"{Base}/Get/{study.Id}")).Status);
        // المهندس يرى الأمر لكنه لا يفتح ولا يعدّل ولا يرسل
        Assert.Equal(HttpStatusCode.OK, (await org.Engineer.Get<StudyDetailDto>($"{Base}/Get/{study.Id}")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Engineer.Post<StudyDetailDto>($"{Base}/Create", new { projectId = project, title = "x" })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Engineer.Put<StudyDetailDto>($"{Base}/Update/{study.Id}", new { projectId = project, title = "x", mode = 1 })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Engineer.Post<StudyDetailDto>($"{Base}/Send/{study.Id}", new { })).Status);
        // رئيس القسم لا يعدّ المخرجات ولا يراجعها
        var architectural = Of(study, org.ArchitecturalTypeId);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Head.Post<StudyDetailDto>($"{Base}/Deliverable/{architectural.Id}/Submit", new { })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Head.Post<StudyDetailDto>($"{Base}/Deliverable/{architectural.Id}/Review", new { approve = true })).Status);

        // قائمة البحث ضمن الحدّ، و«ما أُسند إليّ» للمهندس
        var mine = (await org.Engineer.Get<PagedResultDto<StudyListItemDto>>($"{Base}/Search?mine=true&projectId={project}")).Ok();
        Assert.Contains(mine.Items, s => s.Id == study.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Outsider.Get<PagedResultDto<StudyListItemDto>>($"{Base}/Search?projectId={project}")).Status);   // بلا صلاحية دراسات
    }

    [Fact]
    public async Task An_assignee_must_be_a_project_member_and_only_an_untouched_deliverable_can_change_type_or_be_removed()
    {
        var org = await OrgAsync();
        var project = await NewProjectAsync(org);
        var study = (await org.Head.Post<StudyDetailDto>($"{Base}/Create", new { projectId = project, title = "دراسة" })).Ok();

        // الغريب عن المشروع لا يُسنَد إليه
        var reviewerUserId = (await api.Admin.Get<List<UserResponseDto>>("/api/Users/GetAll")).Ok().First(u => u.Email.StartsWith("rev-")).Id;
        var notMember = await org.Head.Post<StudyDetailDto>($"{Base}/Deliverables/{study.Id}", new { typeId = org.ArchitecturalTypeId, assigneeUserId = reviewerUserId });
        Assert.Equal(HttpStatusCode.BadRequest, notMember.Status);

        study = (await org.Head.Post<StudyDetailDto>($"{Base}/Deliverables/{study.Id}", new { typeId = org.ArchitecturalTypeId, assigneeUserId = org.EngineerId })).Ok();
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Head.Post<StudyDetailDto>($"{Base}/Deliverables/{study.Id}", new { typeId = org.ArchitecturalTypeId })).Status);   // نوع مكرر

        var d = Of(study, org.ArchitecturalTypeId);
        study = (await org.Head.Put<StudyDetailDto>($"{Base}/Deliverable/{d.Id}", new { typeId = org.StructuralTypeId, assigneeUserId = org.EngineerId })).Ok();
        Assert.Equal(org.StructuralTypeId, Assert.Single(study.Deliverables).TypeId);

        // بعد رفع ملفه لا يُحذف
        d = Assert.Single(study.Deliverables);
        study = (await org.Head.Get<StudyDetailDto>($"{Base}/Get/{study.Id}")).Ok();
        await PrepareAndSubmitAsync(org.Engineer, project, study, org.StructuralTypeId);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Head.Delete($"{Base}/Deliverable/{d.Id}")).Status);
    }

    [Fact]
    public async Task A_document_that_is_a_deliverable_file_cannot_be_deleted_from_the_dossier()
    {
        var org = await OrgAsync();
        var project = await NewProjectAsync(org);
        var study = await NewStudyAsync(org, project);
        study = await PrepareAndSubmitAsync(org.Engineer, project, study, org.ArchitecturalTypeId);
        var documentId = Of(study, org.ArchitecturalTypeId).DocumentId;
        Assert.NotNull(documentId);

        var delete = await api.Admin.Delete($"/api/ProjectDocuments/Delete/{documentId}");
        Assert.Equal(HttpStatusCode.BadRequest, delete.Status);
    }

    [Fact]
    public async Task Cancelling_is_by_decision_and_a_cancelled_study_is_frozen()
    {
        var org = await OrgAsync();
        var project = await NewProjectAsync(org);
        var study = await NewStudyAsync(org, project);

        study = (await org.Head.Post<StudyDetailDto>($"{Base}/Cancel/{study.Id}", new { rowVersion = study.RowVersion })).Ok();
        Assert.Equal((int)StudyStatus.Cancelled, study.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Head.Post<StudyDetailDto>($"{Base}/Deliverables/{study.Id}", new { typeId = org.ArchitecturalTypeId })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Head.Post<StudyDetailDto>($"{Base}/Cancel/{study.Id}", new { })).Status);
    }

    // ───────── التنبيه والتقرير ─────────

    [Fact]
    public async Task An_overdue_deliverable_is_notified_once_per_due_date_and_nothing_changes_by_time()
    {
        var org = await OrgAsync();
        var project = await NewProjectAsync(org);
        var study = (await org.Head.Post<StudyDetailDto>($"{Base}/Create", new { projectId = project, title = "دراسة متأخرة", mode = 2 })).Ok();
        study = (await org.Head.Post<StudyDetailDto>($"{Base}/Deliverables/{study.Id}",
            new { typeId = org.ArchitecturalTypeId, assigneeUserId = org.EngineerId, dueDate = DateTime.Today.AddDays(-3) })).Ok();
        var d = Of(study, org.ArchitecturalTypeId);
        Assert.True(d.IsOverdue);

        async Task<int> NoticesAsync()
        {
            await using var scope = api.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<DataContext>().Notifications.AsNoTracking()
                .CountAsync(n => n.Type == NotificationType.StudyDeliverableOverdue && n.RelatedEntityId == study.Id && n.UserId == org.EngineerId);
        }
        async Task RunAsync()
        {
            await using var scope = api.Services.CreateAsyncScope();
            var sp = scope.ServiceProvider;
            await StudyReminders.RunAsync(sp.GetRequiredService<IStudyService>(), sp.GetRequiredService<INotificationService>(), DateTime.Now);
        }

        await RunAsync();
        Assert.Equal(1, await NoticesAsync());
        await RunAsync();
        Assert.Equal(1, await NoticesAsync());                                  // مرة لكل موعد

        d = Of((await org.Head.Get<StudyDetailDto>($"{Base}/Get/{study.Id}")).Ok(), org.ArchitecturalTypeId);   // أعاد التنبيه كتابة المخرج فتغيّر رقم إصداره
        study = (await org.Head.Put<StudyDetailDto>($"{Base}/Deliverable/{d.Id}",
            new { typeId = org.ArchitecturalTypeId, assigneeUserId = org.EngineerId, dueDate = DateTime.Today.AddDays(-1), rowVersion = d.RowVersion })).Ok();
        await RunAsync();
        Assert.Equal(2, await NoticesAsync());                                  // موعد جديد: تنبيه جديد
        Assert.Equal((int)DeliverableState.Preparing, Of(study, org.ArchitecturalTypeId).State);   // لا قرار بمرور الوقت
        Assert.Equal((int)StudyStatus.Open, study.Status);
    }

    [Fact]
    public async Task The_studies_report_lists_the_visible_orders_with_their_progress()
    {
        var org = await OrgAsync();
        var project = await NewProjectAsync(org);
        var marker = "دراسة تقرير " + Guid.NewGuid().ToString("N")[..8];
        var study = (await org.Head.Post<StudyDetailDto>($"{Base}/Create", new { projectId = project, title = marker })).Ok();

        var preview = (await api.Admin.Post<ReportPreviewDto>("/api/Reports/Preview/studies", new { filters = new Dictionary<string, string?> { ["text"] = marker } })).Ok();
        var row = Assert.Single(preview.Table.Rows);
        Assert.Equal(study.Number, ((JsonElement)row[0]!).ToString());
        Assert.Equal(marker, ((JsonElement)row[1]!).ToString());

        // من لا يملك صلاحية تصدير التقرير يُمنع
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Head.Post<ReportPreviewDto>("/api/Reports/Preview/studies", new { filters = new Dictionary<string, string?>() })).Status);
    }
}
