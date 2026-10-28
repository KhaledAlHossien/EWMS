using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using static Application.Features.Studies.StudySupport;

namespace Application.Features.Studies
{
    // مخرجات أمر الدراسة: إضافتها وإسنادها (رئيس القسم)، وإعدادها ورفعها للمراجعة (المهندس المسؤول)،
    // ومراجعتها (غير المُعدّ)، وإعادة المعتمد منها لجولة جديدة (رئيس القسم). كل عملية تعيد الأمر كاملاً.

    public record AddStudyDeliverableCommand(int StudyOrderId, StudyDeliverableRequestDto Dto) : IRequest<StudyDetailDto>;
    public record UpdateStudyDeliverableCommand(int Id, StudyDeliverableRequestDto Dto) : IRequest<StudyDetailDto>;
    public record RemoveStudyDeliverableCommand(int Id) : IRequest<StudyDetailDto>;
    public record AttachDeliverableDocumentCommand(int Id, AttachDeliverableDocumentRequestDto Dto) : IRequest<StudyDetailDto>;
    public record SubmitDeliverableCommand(int Id, byte[]? RowVersion) : IRequest<StudyDetailDto>;
    public record ReviewDeliverableCommand(int Id, ReviewDeliverableRequestDto Dto) : IRequest<StudyDetailDto>;
    public record SendBackDeliverableCommand(int Id, SendBackDeliverableRequestDto Dto) : IRequest<StudyDetailDto>;

    internal static class DeliverableChecks
    {
        public static IEnumerable<string> Problems(StudyDeliverableRequestDto dto)
        {
            if ((dto.Note ?? string.Empty).Length > StudyRules.MaxNoteLength) yield return $"الملاحظة لا تتجاوز {StudyRules.MaxNoteLength} حرفاً";
            if (dto.SortOrder is < 0 or > 1000) yield return "الترتيب بين 0 و1000";
        }
    }

    public class AddStudyDeliverableCommandValidator : AbstractValidator<AddStudyDeliverableCommand>
    {
        public AddStudyDeliverableCommandValidator() =>
            RuleFor(x => x.Dto).Custom((dto, c) => { foreach (var problem in DeliverableChecks.Problems(dto)) c.AddFailure(problem); });
    }

    public class UpdateStudyDeliverableCommandValidator : AbstractValidator<UpdateStudyDeliverableCommand>
    {
        public UpdateStudyDeliverableCommandValidator() =>
            RuleFor(x => x.Dto).Custom((dto, c) => { foreach (var problem in DeliverableChecks.Problems(dto)) c.AddFailure(problem); });
    }

    public class ReviewDeliverableCommandValidator : AbstractValidator<ReviewDeliverableCommand>
    {
        public ReviewDeliverableCommandValidator() =>
            RuleFor(x => x.Dto.Note).MaximumLength(StudyRules.MaxNoteLength).WithMessage($"السبب لا يتجاوز {StudyRules.MaxNoteLength} حرفاً");
    }

    public class AddStudyDeliverableHandler(StudyContext ctx) : IRequestHandler<AddStudyDeliverableCommand, StudyDetailDto>
    {
        public async Task<StudyDetailDto> Handle(AddStudyDeliverableCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var order = await ctx.LoadAsync(viewer, request.StudyOrderId);
            StudyRules.EnsureCanEdit(viewer, order);

            var type = await ctx.Studies.GetTypeAsync(dto.TypeId) ?? throw new KeyNotFoundException("نوع المخرج غير موجود");
            StudyRules.EnsureTypeUsable(type, order.Project.BranchId);
            StudyRules.EnsureCanAddDeliverable(order, type.Id);
            var (assignee, office) = await ctx.ResolveAssignmentAsync(order.Project, dto.AssigneeUserId, dto.OfficeId);

            var deliverable = new StudyDeliverable
            {
                TypeId = type.Id,
                OfficeId = office?.Id,
                AssigneeUserId = assignee?.Id,
                DueDate = dto.DueDate?.Date,
                Note = (dto.Note ?? string.Empty).Trim(),
                SortOrder = dto.SortOrder ?? (order.Deliverables.Count == 0 ? 1 : order.Deliverables.Max(d => d.SortOrder) + 1),
                State = DeliverableState.Preparing
            };
            await ctx.Studies.AddDeliverableAsync(order, deliverable, new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.Deliverable, null,
                $"إضافة مخرج «{type.Name}» إلى أمر الدراسة {StudyContext.Label(order)}",
                Lines(assignee is null ? null : $"المسؤول: {assignee.FullName}", office is null ? null : $"المكتب: {office.Name}")));

            if (assignee != null)
                await ctx.NotifyAsync([assignee.Id], viewer.Id, order, NotificationType.StudyDeliverableAssigned, "أُسند إليك مخرج دراسة",
                    $"أُسند إليك مخرج «{type.Name}» في أمر الدراسة {StudyContext.Label(order)}");
            return await ctx.DetailAsync(viewer, order.Id);
        }
    }

    public class UpdateStudyDeliverableHandler(StudyContext ctx) : IRequestHandler<UpdateStudyDeliverableCommand, StudyDetailDto>
    {
        public async Task<StudyDetailDto> Handle(UpdateStudyDeliverableCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var (order, d) = await ctx.LoadDeliverableAsync(viewer, request.Id);
            StudyRules.EnsureCanEdit(viewer, order);
            if (d.State is DeliverableState.InReview) throw new InvalidOperationException("المخرج بانتظار المراجعة: لا يُعدَّل حتى يبتّ فيه المراجع");
            if (d.State is DeliverableState.Approved) throw new InvalidOperationException("المخرج معتمد: أعده لجولة جديدة قبل تعديل إسناده");

            if (dto.TypeId != d.TypeId)
            {
                StudyRules.EnsureCanRemoveDeliverable(d);   // النوع يتغير قبل بدء العمل فقط
                var type = await ctx.Studies.GetTypeAsync(dto.TypeId) ?? throw new KeyNotFoundException("نوع المخرج غير موجود");
                StudyRules.EnsureTypeUsable(type, order.Project.BranchId);
                StudyRules.EnsureCanAddDeliverable(order, type.Id);
                d.TypeId = type.Id;
                d.Type = type;
            }
            var (assignee, office) = await ctx.ResolveAssignmentAsync(order.Project, dto.AssigneeUserId, dto.OfficeId);

            var details = Lines(
                Change("المسؤول", d.Assignee?.FullName, assignee?.FullName),
                Change("المكتب", d.Office?.Name, office?.Name),
                Change("الموعد", d.DueDate?.ToString("yyyy-MM-dd"), dto.DueDate?.ToString("yyyy-MM-dd")),
                Change("الملاحظة", d.Note, dto.Note),
                dto.SortOrder is int sort && sort != d.SortOrder ? $"الترتيب: {d.SortOrder} ← {sort}" : null);

            var assigneeChanged = assignee?.Id != d.AssigneeUserId;
            d.AssigneeUserId = assignee?.Id;
            d.Assignee = assignee;
            d.OfficeId = office?.Id;
            d.Office = office;
            d.DueDate = dto.DueDate?.Date;
            d.Note = (dto.Note ?? string.Empty).Trim();
            if (dto.SortOrder is int newSort) d.SortOrder = newSort;

            await ctx.Studies.UpdateDeliverableAsync(order, d, dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.Deliverable, d.Id, $"تعديل المخرج «{d.Type.Name}» في {StudyContext.Label(order)}", details));

            if (assigneeChanged && assignee != null)
                await ctx.NotifyAsync([assignee.Id], viewer.Id, order, NotificationType.StudyDeliverableAssigned, "أُسند إليك مخرج دراسة",
                    $"أُسند إليك مخرج «{d.Type.Name}» في أمر الدراسة {StudyContext.Label(order)}");
            return await ctx.DetailAsync(viewer, order.Id);
        }
    }

    public class RemoveStudyDeliverableHandler(StudyContext ctx) : IRequestHandler<RemoveStudyDeliverableCommand, StudyDetailDto>
    {
        public async Task<StudyDetailDto> Handle(RemoveStudyDeliverableCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var (order, d) = await ctx.LoadDeliverableAsync(viewer, request.Id);
            StudyRules.EnsureCanEdit(viewer, order);
            StudyRules.EnsureCanRemoveDeliverable(d);

            var name = d.Type.Name;
            await ctx.Studies.RemoveDeliverableAsync(order, d,
                new AuditEntry(viewer.Id, AuditActions.Deleted, AuditEntities.Deliverable, d.Id, $"حذف المخرج «{name}» من {StudyContext.Label(order)}"));
            return await ctx.DetailAsync(viewer, order.Id);
        }
    }

    /// <summary>
    /// ربط وثيقة المخرج: المُعِدّ يرفع الملف من نافذة الوثائق (الرفع المجزّأ) ثم يربطه هنا؛ الإصدارات اللاحقة تُضاف إلى الوثيقة نفسها.
    /// </summary>
    public class AttachDeliverableDocumentHandler(StudyContext ctx) : IRequestHandler<AttachDeliverableDocumentCommand, StudyDetailDto>
    {
        public async Task<StudyDetailDto> Handle(AttachDeliverableDocumentCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var (order, d) = await ctx.LoadDeliverableAsync(viewer, request.Id);
            StudyRules.EnsureCanPrepare(viewer, order, d);
            if (d.DocumentId != null) throw new InvalidOperationException("ملف المخرج مربوط مسبقاً: أضف إصداراً جديداً إلى الوثيقة نفسها");

            var document = await ctx.Studies.GetDocumentAsync(request.Dto.DocumentId) ?? throw new KeyNotFoundException("الوثيقة غير موجودة");
            if (document.ProjectId != order.ProjectId) throw new InvalidOperationException("الوثيقة من مشروع آخر");
            if (document.Versions.Count == 0) throw new InvalidOperationException("الوثيقة بلا ملف");
            if (!viewer.IsSuperAdmin && document.CreatedByUserId != viewer.Id) throw new UnauthorizedAccessException("اربط وثيقة رفعتَها أنت");
            if (await ctx.Studies.DocumentInUseAsync(document.Id)) throw new InvalidOperationException("الوثيقة مربوطة بمخرج آخر");

            d.DocumentId = document.Id;
            await ctx.Studies.UpdateDeliverableAsync(order, d, request.Dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Attached, AuditEntities.Deliverable, d.Id, $"ربط ملف بالمخرج «{d.Type.Name}» في {StudyContext.Label(order)}", $"«{document.Title}»"));
            return await ctx.DetailAsync(viewer, order.Id);
        }
    }

    /// <summary>الرفع للمراجعة: يُثبَّت أحدث إصدار، ويتجمّد المخرج حتى يبتّ فيه المراجع، ويُشعَر حاملو المراجعة</summary>
    public class SubmitDeliverableHandler(StudyContext ctx) : IRequestHandler<SubmitDeliverableCommand, StudyDetailDto>
    {
        public async Task<StudyDetailDto> Handle(SubmitDeliverableCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var (order, d) = await ctx.LoadDeliverableAsync(viewer, request.Id);
            var latest = d.Document?.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
            StudyRules.EnsureCanSubmitForReview(viewer, order, d, latest?.Id);

            d.State = DeliverableState.InReview;
            d.SubmittedVersionId = latest!.Id;
            d.SubmittedByUserId = viewer.Id;
            d.SubmittedAt = DateTime.UtcNow;
            d.ReturnNote = null;
            d.ReviewedByUserId = null;
            d.ReviewedAt = null;

            await ctx.Studies.UpdateDeliverableAsync(order, d, request.RowVersion, new AuditEntry(viewer.Id, AuditActions.Submitted, AuditEntities.Deliverable, d.Id,
                $"رفع المخرج «{d.Type.Name}» للمراجعة في {StudyContext.Label(order)}", $"الإصدار {latest.VersionNumber}: {latest.FileName}"));

            var reviewers = await ctx.HoldersAsync(order.Project, AppPermissions.ReviewStudyDeliverable);
            await ctx.NotifyAsync(reviewers, viewer.Id, order, NotificationType.StudyReviewRequested, "مخرج دراسة بانتظار مراجعتك",
                $"رفع {viewer.User.FullName} مخرج «{d.Type.Name}» في {StudyContext.Label(order)} للمراجعة");
            return await ctx.DetailAsync(viewer, order.Id);
        }
    }

    /// <summary>
    /// المراجعة: الاعتماد يثبّت الإصدار المرفوع إصداراً معتمداً (في الإضبارة أيضاً) ولا يُستبدل، والإعادة تشترط سبباً.
    /// يُشعَر من رفع المخرج ومسؤولُه بالنتيجة.
    /// </summary>
    public class ReviewDeliverableHandler(StudyContext ctx) : IRequestHandler<ReviewDeliverableCommand, StudyDetailDto>
    {
        public async Task<StudyDetailDto> Handle(ReviewDeliverableCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var (order, d) = await ctx.LoadDeliverableAsync(viewer, request.Id);
            var submitted = d.SubmittedVersionId is int id ? d.Document?.Versions.FirstOrDefault(v => v.Id == id) : null;
            StudyRules.EnsureCanReview(viewer, order, d, submitted?.UploadedByUserId, dto.Approve, dto.Note);

            d.ReviewedByUserId = viewer.Id;
            d.ReviewedAt = DateTime.UtcNow;
            if (dto.Approve)
            {
                d.State = DeliverableState.Approved;
                d.ApprovedVersionId = d.SubmittedVersionId;
                d.ReturnNote = null;
                await ctx.Studies.ApproveDeliverableAsync(order, d, d.SubmittedVersionId!.Value, viewer.Id, dto.RowVersion,
                    new AuditEntry(viewer.Id, AuditActions.Approved, AuditEntities.Deliverable, d.Id,
                        $"اعتماد المخرج «{d.Type.Name}» في {StudyContext.Label(order)}", $"الإصدار {submitted?.VersionNumber}: {submitted?.FileName}"));
            }
            else
            {
                d.State = DeliverableState.Returned;
                d.ReturnNote = dto.Note!.Trim();
                await ctx.Studies.UpdateDeliverableAsync(order, d, dto.RowVersion, new AuditEntry(viewer.Id, AuditActions.Returned, AuditEntities.Deliverable, d.Id,
                    $"إعادة المخرج «{d.Type.Name}» في {StudyContext.Label(order)}", d.ReturnNote));
            }

            var recipients = new List<int>();
            if (d.SubmittedByUserId is int submitter) recipients.Add(submitter);
            if (d.AssigneeUserId is int assignee) recipients.Add(assignee);
            await ctx.NotifyAsync(recipients, viewer.Id, order, NotificationType.StudyDeliverableDecided,
                dto.Approve ? "اعتُمد مخرج أعددتَه" : "أُعيد إليك مخرج دراسة",
                dto.Approve ? $"اعتمد {viewer.User.FullName} مخرج «{d.Type.Name}» في {StudyContext.Label(order)}"
                            : $"أعاد {viewer.User.FullName} مخرج «{d.Type.Name}» في {StudyContext.Label(order)}: {d.ReturnNote}");
            return await ctx.DetailAsync(viewer, order.Id);
        }
    }

    /// <summary>إعادة مخرج معتمد إلى الإعداد لجولة جديدة (رئيس القسم، بسبب مكتوب). الإصدار المعتمد السابق يبقى في الإضبارة</summary>
    public class SendBackDeliverableHandler(StudyContext ctx) : IRequestHandler<SendBackDeliverableCommand, StudyDetailDto>
    {
        public async Task<StudyDetailDto> Handle(SendBackDeliverableCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var (order, d) = await ctx.LoadDeliverableAsync(viewer, request.Id);
            StudyRules.EnsureCanSendBack(viewer, order, d, dto.Note);

            d.State = DeliverableState.Returned;
            d.ReturnNote = dto.Note.Trim();
            await ctx.Studies.UpdateDeliverableAsync(order, d, dto.RowVersion, new AuditEntry(viewer.Id, AuditActions.Returned, AuditEntities.Deliverable, d.Id,
                $"إعادة المخرج المعتمد «{d.Type.Name}» لجولة جديدة في {StudyContext.Label(order)}", d.ReturnNote));

            if (d.AssigneeUserId is int assignee)
                await ctx.NotifyAsync([assignee], viewer.Id, order, NotificationType.StudyDeliverableDecided, "أُعيد إليك مخرج دراسة لجولة جديدة",
                    $"أعاد {viewer.User.FullName} مخرج «{d.Type.Name}» في {StudyContext.Label(order)}: {d.ReturnNote}");
            return await ctx.DetailAsync(viewer, order.Id);
        }
    }
}
