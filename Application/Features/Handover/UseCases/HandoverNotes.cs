using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using static Application.Features.Handover.HandoverSupport;

namespace Application.Features.Handover
{
    // ملاحظات لجنة الاستلام: تُسجَّل أثناء الكشف أو بعد نتيجة «بملاحظات»، وتُسند إلى مسؤول، وتُتابَع (معالجة ثم تحقق بغير من عالج) حتى تُغلق.

    public record AddHandoverNoteCommand(int HandoverId, HandoverNoteRequestDto Dto) : IRequest<HandoverDetailDto>;
    public record UpdateHandoverNoteCommand(int NoteId, HandoverNoteRequestDto Dto) : IRequest<HandoverDetailDto>;
    public record RemoveHandoverNoteCommand(int NoteId) : IRequest<HandoverDetailDto>;
    public record FixHandoverNoteCommand(int NoteId, FixHandoverNoteRequestDto Dto) : IRequest<HandoverDetailDto>;
    public record VerifyHandoverNoteCommand(int NoteId, VerifyHandoverNoteRequestDto Dto) : IRequest<HandoverDetailDto>;
    public record RejectHandoverFixCommand(int NoteId, VerifyHandoverNoteRequestDto Dto) : IRequest<HandoverDetailDto>;

    internal static class NoteInput
    {
        /// <summary>الوصف والمسؤول والموعد بعد التحقق، وتحميل الموظف المسؤول (نشط ومن فرع المشروع)</summary>
        public static async Task<User?> ResolveResponsibleAsync(HandoverContext ctx, Project project, HandoverNoteRequestDto dto)
        {
            if (HandoverRules.ValidateNote(dto.Description, dto.ResponsibleUserId, dto.ResponsibleName, dto.DueDate, DateTime.Today) is string problem)
                throw new InvalidOperationException(problem);
            var user = dto.ResponsibleUserId is int id ? await ctx.Users.GetByIdAsync(id) ?? throw new KeyNotFoundException("الموظف المسؤول غير موجود") : null;
            if (HandoverRules.ValidateResponsible(project, user) is string invalid) throw new InvalidOperationException(invalid);
            return user;
        }

        public static string? Name(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public class AddHandoverNoteHandler(HandoverContext ctx) : IRequestHandler<AddHandoverNoteCommand, HandoverDetailDto>
    {
        public async Task<HandoverDetailDto> Handle(AddHandoverNoteCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var record = await ctx.LoadAsync(viewer, request.HandoverId);
            HandoverRules.EnsureCanAddNote(viewer, record);
            var responsible = await NoteInput.ResolveResponsibleAsync(ctx, record.Project, dto);

            await ctx.Handovers.AddNoteAsync(record, new HandoverNote
            {
                HandoverRecordId = record.Id, Description = dto.Description.Trim(), ResponsibleUserId = responsible?.Id, ResponsibleName = responsible == null ? NoteInput.Name(dto.ResponsibleName) : null,
                DueDate = dto.DueDate?.Date, Status = HandoverNoteStatus.Open, CreatedAt = DateTime.UtcNow
            }, new AuditEntry(viewer.Id, AuditActions.Recorded, AuditEntities.Handover, record.Id,
                $"ملاحظة على {HandoverContext.Label(record)}: {Shorten(dto.Description)}",
                Lines(responsible is null && NoteInput.Name(dto.ResponsibleName) is null ? null : $"المسؤول: {responsible?.FullName ?? NoteInput.Name(dto.ResponsibleName)}",
                    dto.DueDate is null ? null : $"موعد المعالجة: {dto.DueDate:yyyy-MM-dd}")));

            if (responsible != null)
                await ctx.NotifyAsync([responsible.Id], viewer.Id, record, NotificationType.HandoverNoteAssigned, "أُسندت إليك ملاحظة استلام",
                    $"«{Shorten(dto.Description)}» في {HandoverContext.Label(record)} — المشروع «{record.Project.Name}»");
            return await ctx.DetailAsync(viewer, record.Id);
        }
    }

    public class UpdateHandoverNoteHandler(HandoverContext ctx) : IRequestHandler<UpdateHandoverNoteCommand, HandoverDetailDto>
    {
        public async Task<HandoverDetailDto> Handle(UpdateHandoverNoteCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var (record, note) = await ctx.LoadNoteAsync(viewer, request.NoteId);
            HandoverRules.EnsureCanEditNote(viewer, record, note);
            var responsible = await NoteInput.ResolveResponsibleAsync(ctx, record.Project, dto);
            var newName = responsible == null ? NoteInput.Name(dto.ResponsibleName) : null;

            var details = Lines(
                Change("الوصف", note.Description, dto.Description),
                Change("المسؤول", note.ResponsibleUser?.FullName ?? note.ResponsibleName, responsible?.FullName ?? newName),
                Change("الموعد", note.DueDate?.ToString("yyyy-MM-dd"), dto.DueDate?.ToString("yyyy-MM-dd")));
            var newlyAssigned = responsible != null && responsible.Id != note.ResponsibleUserId;

            note.Description = dto.Description.Trim();
            note.ResponsibleUserId = responsible?.Id;
            note.ResponsibleName = newName;
            note.DueDate = dto.DueDate?.Date;

            await ctx.Handovers.SaveNoteAsync(record, note, dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.Handover, record.Id, $"تعديل ملاحظة على {HandoverContext.Label(record)}", details));
            if (newlyAssigned)
                await ctx.NotifyAsync([responsible!.Id], viewer.Id, record, NotificationType.HandoverNoteAssigned, "أُسندت إليك ملاحظة استلام",
                    $"«{Shorten(dto.Description)}» في {HandoverContext.Label(record)} — المشروع «{record.Project.Name}»");
            return await ctx.DetailAsync(viewer, record.Id);
        }
    }

    public class RemoveHandoverNoteHandler(HandoverContext ctx) : IRequestHandler<RemoveHandoverNoteCommand, HandoverDetailDto>
    {
        public async Task<HandoverDetailDto> Handle(RemoveHandoverNoteCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var (record, note) = await ctx.LoadNoteAsync(viewer, request.NoteId);
            HandoverRules.EnsureCanRemoveNote(viewer, record, note);

            await ctx.Handovers.RemoveNoteAsync(record, note,
                new AuditEntry(viewer.Id, AuditActions.Deleted, AuditEntities.Handover, record.Id, $"حذف ملاحظة من {HandoverContext.Label(record)}: {Shorten(note.Description)}"));
            return await ctx.DetailAsync(viewer, record.Id);
        }
    }

    /// <summary>تسجيل المعالجة: ما عولج ووثيقته (إصدار من الإضبارة)؛ ينتظر التحقق من غير من عالج</summary>
    public class FixHandoverNoteHandler(HandoverContext ctx) : IRequestHandler<FixHandoverNoteCommand, HandoverDetailDto>
    {
        public async Task<HandoverDetailDto> Handle(FixHandoverNoteCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var (record, note) = await ctx.LoadNoteAsync(viewer, request.NoteId);
            HandoverRules.EnsureCanMarkFixed(viewer, record, note, dto.FixNote);
            var evidence = await ctx.ResolveVersionAsync(dto.EvidenceVersionId, record.ProjectId);

            note.Status = HandoverNoteStatus.Fixed;
            note.FixedAt = DateTime.UtcNow;
            note.FixedByUserId = viewer.Id;
            note.FixNote = dto.FixNote.Trim();
            note.EvidenceVersionId = evidence?.Id;
            await ctx.Handovers.SaveNoteAsync(record, note, dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.Handover, record.Id,
                    $"معالجة ملاحظة في {HandoverContext.Label(record)}: {Shorten(note.Description)}", $"ما عولج: {dto.FixNote.Trim()}"));

            await ctx.NotifyAsync(await ctx.HoldersAsync(record.Project, AppPermissions.FollowHandoverNotes), viewer.Id, record, NotificationType.HandoverNoteFixed,
                "ملاحظة استلام تنتظر التحقق", $"عولجت «{Shorten(note.Description)}» في {HandoverContext.Label(record)} — المشروع «{record.Project.Name}»");
            return await ctx.DetailAsync(viewer, record.Id);
        }
    }

    /// <summary>التحقق من المعالجة وإغلاق الملاحظة (غير من عالجها، حتى مدير النظام)</summary>
    public class VerifyHandoverNoteHandler(HandoverContext ctx) : IRequestHandler<VerifyHandoverNoteCommand, HandoverDetailDto>
    {
        public async Task<HandoverDetailDto> Handle(VerifyHandoverNoteCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var (record, note) = await ctx.LoadNoteAsync(viewer, request.NoteId);
            HandoverRules.EnsureCanVerify(viewer, record, note);
            if ((dto.Note ?? string.Empty).Length > 1000) throw new InvalidOperationException("الملاحظة لا تتجاوز 1000 حرف");
            var evidence = await ctx.ResolveVersionAsync(dto.EvidenceVersionId, record.ProjectId);

            note.Status = HandoverNoteStatus.Verified;
            note.VerifiedAt = DateTime.UtcNow;
            note.VerifiedByUserId = viewer.Id;
            if (evidence != null) note.EvidenceVersionId = evidence.Id;
            await ctx.Handovers.SaveNoteAsync(record, note, dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Closed, AuditEntities.Handover, record.Id,
                    $"التحقق من معالجة ملاحظة في {HandoverContext.Label(record)}: {Shorten(note.Description)}",
                    string.IsNullOrWhiteSpace(dto.Note) ? string.Empty : $"الملاحظة: {dto.Note.Trim()}"));
            return await ctx.DetailAsync(viewer, record.Id);
        }
    }

    /// <summary>رفض المعالجة عند التحقق: تعود الملاحظة مفتوحة بسبب مكتوب (يُحفظ في الأثر وإشعار من عالجها)</summary>
    public class RejectHandoverFixHandler(HandoverContext ctx) : IRequestHandler<RejectHandoverFixCommand, HandoverDetailDto>
    {
        public async Task<HandoverDetailDto> Handle(RejectHandoverFixCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var (record, note) = await ctx.LoadNoteAsync(viewer, request.NoteId);
            HandoverRules.EnsureCanRejectFix(viewer, record, note, dto.Note);
            var fixer = note.FixedByUserId;

            note.Status = HandoverNoteStatus.Open;
            note.FixedAt = null;
            note.FixedByUserId = null;
            note.FixNote = string.Empty;
            note.EvidenceVersionId = null;
            await ctx.Handovers.SaveNoteAsync(record, note, dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Rejected, AuditEntities.Handover, record.Id,
                    $"رفض معالجة ملاحظة في {HandoverContext.Label(record)}: {Shorten(note.Description)}", $"السبب: {dto.Note!.Trim()}"));

            var recipients = new List<int>();
            if (fixer is int f) recipients.Add(f);
            if (note.ResponsibleUserId is int r) recipients.Add(r);
            await ctx.NotifyAsync(recipients, viewer.Id, record, NotificationType.HandoverNoteAssigned, "أُعيدت إليك معالجة ملاحظة استلام",
                $"«{Shorten(note.Description)}» في {HandoverContext.Label(record)}: {dto.Note.Trim()}");
            return await ctx.DetailAsync(viewer, record.Id);
        }
    }
}
