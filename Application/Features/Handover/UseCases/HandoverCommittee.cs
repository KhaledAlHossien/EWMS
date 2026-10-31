using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Contracts;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using static Application.Features.Handover.HandoverSupport;

namespace Application.Features.Handover
{
    // لجنة الاستلام ونتيجته: تشكيل اللجنة (ينقل الطلب إلى «لجنة مشكَّلة»)، تعديلها قبل النتيجة، ثم تسجيل الكشف والنتيجة (ينتهي المحضر).

    public record FormHandoverCommitteeCommand(int HandoverId, CommitteeRequestDto Dto) : IRequest<HandoverDetailDto>;
    public record UpdateHandoverCommitteeCommand(int HandoverId, CommitteeRequestDto Dto) : IRequest<HandoverDetailDto>;
    public record ConcludeHandoverCommand(int HandoverId, ConcludeHandoverRequestDto Dto) : IRequest<HandoverDetailDto>;

    public class FormHandoverCommitteeHandler(HandoverContext ctx) : IRequestHandler<FormHandoverCommitteeCommand, HandoverDetailDto>
    {
        public async Task<HandoverDetailDto> Handle(FormHandoverCommitteeCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var record = await ctx.LoadAsync(viewer, request.HandoverId);
            HandoverRules.EnsureCanFormCommittee(viewer, record);
            var members = await CommitteeInput.ResolveMembersAsync(ctx.Users, record.Project.BranchId, dto);
            var decision = await ctx.ResolveVersionAsync(dto.DecisionVersionId, record.ProjectId);

            var committee = new Committee
            {
                ProjectId = record.ProjectId, Kind = CommitteeKind.Handover, HandoverRecordId = record.Id, FormedAt = dto.FormedAt.Date,
                DecisionNumber = CommitteeInput.Clean(dto.DecisionNumber), DecisionVersionId = decision?.Id,
                Recommendations = (dto.Recommendations ?? string.Empty).Trim(), CreatedByUserId = viewer.Id, CreatedAt = DateTime.UtcNow, Members = members
            };
            record.Status = HandoverStatus.CommitteeFormed;
            await ctx.Handovers.SaveCommitteeAsync(record, committee, isNew: true, [], [], dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.Handover, record.Id,
                    $"تشكيل لجنة الاستلام لـ{HandoverContext.Label(record)}", $"الأعضاء: {CommitteeInput.Describe(members)}"));

            await ctx.NotifyAsync([record.RequestedByUserId], viewer.Id, record, NotificationType.HandoverRequested,
                "شُكّلت لجنة استلام لطلبك", $"شُكّلت لجنة الاستلام لـ{HandoverContext.Label(record)} في المشروع «{record.Project.Name}»");
            return await ctx.DetailAsync(viewer, record.Id);
        }
    }

    /// <summary>تعديل اللجنة قبل النتيجة: يُستبدل أعضاؤها بالقائمة المرسلة، والأثر يحفظ ما كان وما صار</summary>
    public class UpdateHandoverCommitteeHandler(HandoverContext ctx) : IRequestHandler<UpdateHandoverCommitteeCommand, HandoverDetailDto>
    {
        public async Task<HandoverDetailDto> Handle(UpdateHandoverCommitteeCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var record = await ctx.LoadAsync(viewer, request.HandoverId);
            HandoverRules.EnsureCanEditCommittee(viewer, record);
            var committee = record.Committee ?? throw new InvalidOperationException("لم تُشكَّل لجنة لهذا الطلب");
            var members = await CommitteeInput.ResolveMembersAsync(ctx.Users, record.Project.BranchId, dto);
            var decision = await ctx.ResolveVersionAsync(dto.DecisionVersionId, record.ProjectId);

            var removed = committee.Members.ToList();
            var details = Lines(
                $"الأعضاء قبل: {CommitteeInput.Describe(removed)}", $"الأعضاء بعد: {CommitteeInput.Describe(members)}",
                Change("رقم القرار", committee.DecisionNumber, dto.DecisionNumber), Change("التوصيات", committee.Recommendations, dto.Recommendations),
                Change("تاريخ التشكيل", committee.FormedAt.ToString("yyyy-MM-dd"), dto.FormedAt.ToString("yyyy-MM-dd")));

            committee.FormedAt = dto.FormedAt.Date;
            committee.DecisionNumber = CommitteeInput.Clean(dto.DecisionNumber);
            committee.DecisionVersionId = decision?.Id;
            committee.Recommendations = (dto.Recommendations ?? string.Empty).Trim();
            foreach (var member in members) member.CommitteeId = committee.Id;

            await ctx.Handovers.SaveCommitteeAsync(record, committee, isNew: false, members, removed, dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.Handover, record.Id, $"تعديل لجنة الاستلام لـ{HandoverContext.Label(record)}", details));
            return await ctx.DetailAsync(viewer, record.Id);
        }
    }

    /// <summary>
    /// تسجيل الكشف والنتيجة: «بملاحظات» تشترط ملاحظة مسجَّلة، و«بلا ملاحظات» لا تُقبل وفي المحضر (أو في استلام نهائي: في المشروع) ملاحظات لم تُغلق.
    /// محضر اللجنة وتقرير الإشراف إصداران مثبَّتان من الإضبارة.
    /// </summary>
    public class ConcludeHandoverHandler(HandoverContext ctx) : IRequestHandler<ConcludeHandoverCommand, HandoverDetailDto>
    {
        public async Task<HandoverDetailDto> Handle(ConcludeHandoverCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var record = await ctx.LoadAsync(viewer, request.HandoverId);
            var result = (HandoverResult)dto.Result;
            var otherOpen = await ctx.Handovers.CountOpenNotesElsewhereAsync(record.ProjectId, record.Id);
            HandoverRules.EnsureCanConclude(viewer, record, result, dto.InspectionDate, otherOpen, dto.ResultNote, DateTime.Today);
            var minutes = await ctx.ResolveVersionAsync(dto.MinutesVersionId, record.ProjectId);
            var report = await ctx.ResolveVersionAsync(dto.SupervisionReportVersionId, record.ProjectId);

            record.Status = HandoverStatus.Concluded;
            record.Result = result;
            record.InspectionDate = dto.InspectionDate.Date;
            record.ResultNote = (dto.ResultNote ?? string.Empty).Trim();
            record.MinutesVersionId = minutes?.Id;
            record.SupervisionReportVersionId = report?.Id;
            record.ConcludedByUserId = viewer.Id;
            record.ConcludedAt = DateTime.UtcNow;

            await ctx.Handovers.UpdateAsync(record, dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Closed, AuditEntities.Handover, record.Id,
                    $"نتيجة {HandoverContext.Label(record)}: {HandoverRules.ResultAr(result)}",
                    Lines($"تاريخ الكشف: {dto.InspectionDate:yyyy-MM-dd}", result == HandoverResult.WithNotes ? $"عدد الملاحظات: {record.Notes.Count}" : null,
                        minutes is null ? null : "محضر اللجنة مثبَّت من الإضبارة", report is null ? null : "تقرير الإشراف مثبَّت من الإضبارة")));

            await ctx.NotifyAsync([record.RequestedByUserId], viewer.Id, record, NotificationType.HandoverConcluded,
                "سُجّلت نتيجة طلب الاستلام", $"{HandoverContext.Label(record)} في المشروع «{record.Project.Name}»: {HandoverRules.ResultAr(result)}");
            return await ctx.DetailAsync(viewer, record.Id);
        }
    }
}
