using Application.Common;
using Application.DTOs.Response;
using Application.Features.Contracts;
using Domain.Entities;

namespace Application.Features.Handover
{
    /// <summary>
    /// الاستلام: ربط الكيانات بالـ DTOs وحساب أعلام الأزرار بالقواعد نفسها التي يفرضها الخادم
    /// (فلا تتكرر القواعد في الواجهة، وما يظهر زراً هو ما ينجح).
    /// </summary>
    public static class HandoverSupport
    {
        /// <summary>سطر «التسمية: قديم ← جديد» للأثر، أو null إن لم يتغير</summary>
        public static string? Change(string label, string? from, string? to) => ContractSupport.Change(label, from, to);

        public static string Lines(params string?[] changes) => ContractSupport.Lines(changes);

        /// <summary>نص قصير لعنوان الأثر والإشعار</summary>
        public static string Shorten(string? text, int max = 60)
        {
            var t = (text ?? string.Empty).Trim().ReplaceLineEndings(" ");
            return t.Length <= max ? t : t[..(max - 1)] + "…";
        }

        private static bool Allowed(Action guard)
        {
            try { guard(); return true; }
            catch (Exception e) when (e is UnauthorizedAccessException or InvalidOperationException) { return false; }
        }

        private static T ToListItem<T>(HandoverRecord h, DateTime today) where T : HandoverListItemDto, new() => new()
        {
            Id = h.Id,
            Number = HandoverRules.Number(h.Id, h.CreatedAt),
            ProjectId = h.ProjectId,
            ProjectName = h.Project?.Name ?? string.Empty,
            BranchId = h.Project?.BranchId ?? 0,
            ContractId = h.ContractId,
            ContractNumber = h.Contract is null ? null : ContractRules.Number(h.Contract.Id, h.Contract.CreatedAt),
            Kind = (int)h.Kind,
            KindAr = HandoverRules.KindAr(h.Kind),
            Status = (int)h.Status,
            StatusAr = HandoverRules.StatusAr(h.Status),
            Result = h.Result is HandoverResult r ? (int)r : null,
            ResultAr = HandoverRules.ResultAr(h.Result),
            RequestedAt = h.RequestedAt,
            InspectionDate = h.InspectionDate,
            TotalNotes = h.Notes.Count,
            OpenNotes = HandoverRules.OpenNotes(h.Notes),
            HasOverdueNote = h.Notes.Any(n => HandoverRules.IsNoteOverdue(n, today)),
            CreatedAt = h.CreatedAt
        };

        public static HandoverListItemDto ToListItem(HandoverRecord h, DateTime today) => ToListItem<HandoverListItemDto>(h, today);

        public static HandoverDetailDto ToDetail(HandoverRecord h, Viewer viewer, DateTime today)
        {
            var dto = ToListItem<HandoverDetailDto>(h, today);
            dto.RequestedByName = h.RequestedByUser?.FullName ?? string.Empty;
            dto.RequestNote = h.RequestNote;
            dto.CorrespondenceId = h.CorrespondenceId;
            dto.CorrespondenceNumber = h.Correspondence is null ? null : Diwan.DiwanRules.Number(h.Correspondence.Direction, h.Correspondence.Year, h.Correspondence.Sequence);
            dto.ResultNote = h.ResultNote;
            dto.MinutesVersionId = h.MinutesVersionId;
            dto.SupervisionReportVersionId = h.SupervisionReportVersionId;
            dto.ConcludedByName = h.ConcludedByUser?.FullName;
            dto.ConcludedAt = h.ConcludedAt;
            dto.RowVersion = h.RowVersion;

            dto.CanFormCommittee = Allowed(() => HandoverRules.EnsureCanFormCommittee(viewer, h));
            dto.CanEditCommittee = Allowed(() => HandoverRules.EnsureCanEditCommittee(viewer, h));
            var expected = h.Notes.Count > 0 ? HandoverResult.WithNotes : HandoverResult.WithoutNotes;
            dto.CanConclude = Allowed(() => HandoverRules.EnsureCanConclude(viewer, h, expected, today, 0, null, today));
            dto.CanCancel = Allowed(() => HandoverRules.EnsureCanCancel(viewer, h));
            dto.CanAddNote = Allowed(() => HandoverRules.EnsureCanAddNote(viewer, h));

            dto.Committee = h.Committee is null ? null : ContractSupport.ToCommittee(h.Committee, dto.CanEditCommittee);
            dto.Notes = h.Notes.OrderBy(n => n.Id).Select(n => ToNote(h, n, viewer, today)).ToList();
            return dto;
        }

        private static HandoverNoteDto ToNote(HandoverRecord h, HandoverNote n, Viewer viewer, DateTime today) => new()
        {
            Id = n.Id,
            Description = n.Description,
            ResponsibleUserId = n.ResponsibleUserId,
            ResponsibleName = n.ResponsibleUser?.FullName ?? n.ResponsibleName,
            DueDate = n.DueDate,
            Status = (int)n.Status,
            StatusAr = HandoverRules.NoteStatusAr(n.Status),
            IsOverdue = HandoverRules.IsNoteOverdue(n, today),
            FixedAt = n.FixedAt,
            FixedByName = n.FixedByUser?.FullName,
            FixNote = n.FixNote,
            VerifiedAt = n.VerifiedAt,
            VerifiedByName = n.VerifiedByUser?.FullName,
            EvidenceVersionId = n.EvidenceVersionId,
            CreatedAt = n.CreatedAt,
            CanEdit = Allowed(() => HandoverRules.EnsureCanEditNote(viewer, h, n)),
            CanRemove = Allowed(() => HandoverRules.EnsureCanRemoveNote(viewer, h, n)),
            CanFix = Allowed(() => HandoverRules.EnsureCanMarkFixed(viewer, h, n, "x")),
            CanVerify = Allowed(() => HandoverRules.EnsureCanVerify(viewer, h, n)),
            RowVersion = n.RowVersion
        };
    }
}
