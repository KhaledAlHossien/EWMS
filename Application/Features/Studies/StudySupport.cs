using Application.Common;
using Application.DTOs.Response;
using Application.Features.Projects;
using Domain.Entities;

namespace Application.Features.Studies
{
    /// <summary>
    /// الدراسات: ربط الكيانات بالـ DTOs وحساب أعلام الأزرار بالقواعد نفسها التي يفرضها الخادم
    /// (فلا تتكرر القواعد في الواجهة، وما يظهر زراً هو ما ينجح).
    /// </summary>
    public static class StudySupport
    {
        public static DeliverableTypeResponseDto ToDto(DeliverableType t) => new()
        {
            Id = t.Id, BranchId = t.BranchId, Name = t.Name, DocumentKind = (int)t.DocumentKind, DocumentKindAr = ProjectRules.KindAr(t.DocumentKind),
            SortOrder = t.SortOrder, IsActive = t.IsActive, RowVersion = t.RowVersion
        };

        /// <summary>سطر «التسمية: قديم ← جديد» للأثر، أو null إن لم يتغير</summary>
        public static string? Change(string label, string? from, string? to)
        {
            from = (from ?? string.Empty).Trim();
            to = (to ?? string.Empty).Trim();
            if (string.Equals(from, to, StringComparison.Ordinal)) return null;
            string Show(string v) => v.Length == 0 ? "—" : v.Length > 80 ? v[..79] + "…" : v;
            return $"{label}: {Show(from)} ← {Show(to)}";
        }

        public static string Lines(params string?[] changes) => string.Join("\n", changes.Where(x => !string.IsNullOrEmpty(x)));

        /// <summary>هل تنجح هذه الحراسة للمستخدم؟</summary>
        private static bool Allowed(Action guard)
        {
            try { guard(); return true; }
            catch (Exception e) when (e is UnauthorizedAccessException or InvalidOperationException) { return false; }
        }

        private static T ToListItem<T>(StudyOrder o, DateTime today) where T : StudyListItemDto, new() => new()
        {
            Id = o.Id,
            Number = StudyRules.Number(o.Id, o.CreatedAt),
            ProjectId = o.ProjectId,
            ProjectName = o.Project?.Name ?? string.Empty,
            BranchId = o.Project?.BranchId ?? 0,
            Title = o.Title,
            ExternalNumber = o.ExternalNumber,
            Mode = (int)o.Mode,
            ModeAr = StudyRules.ModeAr(o.Mode),
            Status = (int)o.Status,
            StatusAr = StudyRules.StatusAr(o.Status),
            DueDate = o.DueDate,
            DeliverablesTotal = o.Deliverables.Count,
            DeliverablesApproved = o.Deliverables.Count(d => d.State == DeliverableState.Approved),
            ApprovedPercent = StudyRules.ApprovedPercent(o),
            IsOverdue = o.Deliverables.Any(d => StudyRules.IsOverdueCandidate(o, d, today)),
            CorrespondenceId = o.CorrespondenceId,
            CorrespondenceNumber = o.Correspondence is null ? null : Diwan.DiwanRules.Number(o.Correspondence.Direction, o.Correspondence.Year, o.Correspondence.Sequence),
            CreatedAt = o.CreatedAt
        };

        public static StudyListItemDto ToListItem(StudyOrder o, DateTime today) => ToListItem<StudyListItemDto>(o, today);

        private static ProjectDocumentVersion? Latest(StudyDeliverable d) => d.Document?.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

        private static int? UploaderOfSubmitted(StudyDeliverable d) =>
            d.SubmittedVersionId is int id ? d.Document?.Versions.FirstOrDefault(v => v.Id == id)?.UploadedByUserId : null;

        public static StudyDetailDto ToDetail(StudyOrder o, Viewer viewer, DateTime today)
        {
            var dto = ToListItem<StudyDetailDto>(o, today);
            dto.Notes = o.Notes;
            dto.CreatedByName = o.CreatedByUser?.FullName ?? string.Empty;
            dto.RowVersion = o.RowVersion;

            dto.CanEdit = Allowed(() => StudyRules.EnsureCanEdit(viewer, o));
            dto.CanAddDeliverable = dto.CanEdit && o.Deliverables.Count < StudyRules.MaxDeliverables;
            dto.CanSend = Allowed(() => StudyRules.EnsureCanSend(viewer, o));
            var pending = o.Submissions.FirstOrDefault(s => s.Decision == CenterDecision.Pending);
            dto.CanDecide = pending != null && Allowed(() => StudyRules.EnsureCanDecide(viewer, o, pending, CenterDecision.Approved, null));
            dto.CanReopen = Allowed(() => StudyRules.EnsureCanReopen(viewer, o));
            dto.CanCancel = Allowed(() => StudyRules.EnsureCanCancel(viewer, o));

            dto.Deliverables = o.Deliverables.OrderBy(d => d.SortOrder).ThenBy(d => d.Id).Select(d => ToDeliverable(o, d, viewer, today, dto.CanEdit)).ToList();
            dto.Submissions = o.Submissions.OrderBy(s => s.Round).Select(ToSubmission).ToList();
            return dto;
        }

        private static StudyDeliverableDto ToDeliverable(StudyOrder o, StudyDeliverable d, Viewer viewer, DateTime today, bool canEditOrder)
        {
            var latest = Latest(d);
            var submitted = d.SubmittedVersionId is int sv ? d.Document?.Versions.FirstOrDefault(v => v.Id == sv) : null;
            var approved = d.ApprovedVersionId is int av ? d.Document?.Versions.FirstOrDefault(v => v.Id == av) : null;
            var canPrepare = Allowed(() => StudyRules.EnsureCanPrepare(viewer, o, d));

            return new StudyDeliverableDto
            {
                Id = d.Id,
                TypeId = d.TypeId,
                TypeName = d.Type?.Name ?? string.Empty,
                SortOrder = d.SortOrder,
                OfficeId = d.OfficeId,
                OfficeName = d.Office?.Name,
                AssigneeUserId = d.AssigneeUserId,
                AssigneeName = d.Assignee?.FullName,
                DueDate = d.DueDate,
                Note = d.Note,
                State = (int)d.State,
                StateAr = StudyRules.StateAr(d.State),
                IsOverdue = StudyRules.IsOverdueCandidate(o, d, today),
                DocumentId = d.DocumentId,
                LatestVersionId = latest?.Id,
                LatestVersionNumber = latest?.VersionNumber,
                SubmittedVersionId = d.SubmittedVersionId,
                SubmittedVersionNumber = submitted?.VersionNumber,
                SubmittedByName = d.SubmittedByUser?.FullName,
                SubmittedAt = d.SubmittedAt,
                ReviewedByName = d.ReviewedByUser?.FullName,
                ReviewedAt = d.ReviewedAt,
                ReturnNote = d.ReturnNote,
                ApprovedVersionId = d.ApprovedVersionId,
                ApprovedVersionNumber = approved?.VersionNumber,
                BlockedByName = d.State == DeliverableState.Approved ? null : StudyRules.BlockedBy(o, d)?.Type?.Name,
                CanPrepare = canPrepare,
                CanAttachDocument = canPrepare && d.DocumentId == null,
                CanSubmit = canPrepare && Allowed(() => StudyRules.EnsureCanSubmitForReview(viewer, o, d, latest?.Id)),
                CanReview = Allowed(() => StudyRules.EnsureCanReview(viewer, o, d, UploaderOfSubmitted(d), true, null)),
                CanSendBack = Allowed(() => StudyRules.EnsureCanSendBack(viewer, o, d, "x")),
                CanEdit = canEditOrder,
                CanRemove = canEditOrder && StudyRules.IsUntouched(d),
                RowVersion = d.RowVersion
            };
        }

        private static StudySubmissionDto ToSubmission(StudySubmission s) => new()
        {
            Id = s.Id,
            Round = s.Round,
            SentAt = s.SentAt,
            SentByName = s.SentByUser?.FullName ?? string.Empty,
            OutgoingCorrespondenceId = s.OutgoingCorrespondenceId,
            OutgoingCorrespondenceNumber = s.OutgoingCorrespondence is null ? null
                : Diwan.DiwanRules.Number(s.OutgoingCorrespondence.Direction, s.OutgoingCorrespondence.Year, s.OutgoingCorrespondence.Sequence),
            Note = s.Note,
            Decision = (int)s.Decision,
            DecisionAr = StudyRules.DecisionAr(s.Decision),
            DecidedAt = s.DecidedAt,
            DecidedByName = s.DecidedByUser?.FullName,
            DecisionNote = s.DecisionNote,
            DecisionVersionId = s.DecisionVersionId,
            RowVersion = s.RowVersion
        };
    }
}
