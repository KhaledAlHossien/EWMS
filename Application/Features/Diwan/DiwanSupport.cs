using Application.Common;
using Application.DTOs.Response;
using Domain.Entities;

namespace Application.Features.Diwan
{
    /// <summary>دوال ساكنة مشتركة بين معالجات الديوان: التحويل إلى DTO، وحساب ما يحق للمستخدم فعله، وفرق الحقول للأثر.</summary>
    public static class DiwanSupport
    {
        public static CorrespondenceTypeResponseDto ToDto(CorrespondenceType t) => new()
        {
            Id = t.Id, BranchId = t.BranchId, Name = t.Name, DeadlineDays = t.DeadlineDays, IsActive = t.IsActive, SortOrder = t.SortOrder
        };

        public static CorrespondenceStatusResponseDto ToDto(CorrespondenceStatus s) => new()
        {
            Id = s.Id, BranchId = s.BranchId, Name = s.Name, Stage = (int)s.Stage, StageAr = DiwanRules.StageAr(s.Stage),
            Color = s.Color, IsActive = s.IsActive, SortOrder = s.SortOrder
        };

        private static string? UnitName(Correspondence c) => c.CurrentOffice?.Name ?? c.CurrentDepartment?.Name;

        public static T ToListItem<T>(Correspondence c, DateTime today) where T : CorrespondenceListItemDto, new() => new()
        {
            Id = c.Id,
            Number = DiwanRules.Number(c.Direction, c.Year, c.Sequence),
            Direction = (int)c.Direction,
            DirectionAr = DiwanRules.DirectionAr(c.Direction),
            BranchId = c.BranchId,
            Subject = c.Subject,
            Party = c.Party,
            ExternalNumber = c.ExternalNumber,
            TypeId = c.TypeId,
            TypeName = c.Type?.Name ?? string.Empty,
            StatusId = c.StatusId,
            StatusName = c.Status?.Name,
            StatusColor = c.Status?.Color,
            Stage = c.Status is null ? null : (int)c.Status.Stage,
            OutgoingState = c.OutgoingState is null ? null : (int)c.OutgoingState,
            OutgoingStateAr = c.OutgoingState is null ? null : DiwanRules.OutgoingStateAr(c),
            IsSubmitted = DiwanRules.IsSubmittedDraft(c),
            ProjectId = c.ProjectId,
            ProjectName = c.Project?.Name,
            CurrentUnitName = c.Direction == CorrespondenceDirection.Incoming ? UnitName(c) : null,
            DueDate = c.DueDate,
            IsOverdue = DiwanRules.IsOverdueCandidate(c, today),
            IsClosed = DiwanRules.IsClosed(c),
            RegisteredAt = c.RegisteredAt,
            RegisteredByName = c.RegisteredByUser?.FullName ?? string.Empty
        };

        public static CorrespondenceListItemDto ToListItem(Correspondence c, DateTime today) => ToListItem<CorrespondenceListItemDto>(c, today);

        /// <summary>هل تنجح هذه الحراسة للمستخدم؟ (تحسب أعلام الأزرار بالقواعد نفسها التي يفرضها الخادم)</summary>
        private static bool Allowed(Action guard)
        {
            try { guard(); return true; }
            catch (Exception e) when (e is UnauthorizedAccessException or InvalidOperationException) { return false; }
        }

        public static CorrespondenceResponseDto ToDetail(Correspondence c, Viewer viewer, DateTime today)
        {
            var dto = ToListItem<CorrespondenceResponseDto>(c, today);
            var incoming = DiwanRules.IsIncoming(c);
            var open = !DiwanRules.IsClosed(c);

            dto.Summary = c.Summary;
            dto.ExternalDate = c.ExternalDate;
            dto.ReplyToId = c.ReplyToId;
            dto.ReplyToNumber = c.ReplyTo is null ? null : DiwanRules.Number(c.ReplyTo.Direction, c.ReplyTo.Year, c.ReplyTo.Sequence);
            dto.CurrentDepartmentId = c.CurrentDepartmentId;
            dto.CurrentOfficeId = c.CurrentOfficeId;
            dto.CurrentUserId = c.CurrentUserId;
            dto.CurrentUserName = c.CurrentUser?.FullName;
            dto.ReturnNote = c.ReturnNote;
            dto.SubmittedAt = c.SubmittedAt;
            dto.ApprovedByName = c.ApprovedByUser?.FullName;
            dto.ApprovedAt = c.ApprovedAt;
            dto.IssuedAt = c.IssuedAt;
            dto.IssuedByName = c.IssuedByUser?.FullName;
            dto.ClosedAt = c.ClosedAt;
            dto.RowVersion = c.RowVersion;

            dto.Referrals = c.Referrals.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).Select(r => new CorrespondenceReferralDto
            {
                Id = r.Id,
                FromUserId = r.FromUserId,
                FromUserName = r.FromUser?.FullName ?? string.Empty,
                FromDepartmentName = r.FromDepartment?.Name,
                ToDepartmentId = r.ToDepartmentId,
                ToDepartmentName = r.ToDepartment?.Name ?? string.Empty,
                ToOfficeId = r.ToOfficeId,
                ToOfficeName = r.ToOffice?.Name,
                ToUserId = r.ToUserId,
                ToUserName = r.ToUser?.FullName,
                Note = r.Note,
                CreatedAt = r.CreatedAt,
                AcknowledgedAt = r.AcknowledgedAt,
                AcknowledgedByName = r.AcknowledgedByUser?.FullName
            }).ToList();

            dto.Attachments = c.Attachments.OrderBy(a => a.UploadedAt).ThenBy(a => a.Id).Select(a => new CorrespondenceAttachmentDto
            {
                Id = a.Id, FileName = a.FileName, ContentType = a.ContentType, Size = a.Size,
                UploadedByName = a.UploadedByUser?.FullName ?? string.Empty, UploadedAt = a.UploadedAt
            }).ToList();

            var handles = DiwanRules.CanHandle(viewer, c);
            dto.CanEdit = incoming
                ? open && (handles || (viewer.Has(AppPermissions.RegisterCorrespondence) && c.RegisteredByUserId == viewer.Id))
                : Allowed(() => DiwanRules.EnsureCanEditDraft(viewer, c));
            dto.CanRefer = Allowed(() => DiwanRules.EnsureCanRefer(viewer, c));
            dto.CanAcknowledge = incoming && open && handles && DiwanRules.PendingAcknowledgement(c) != null;
            dto.CanChangeStatus = incoming && handles && (open || viewer.IsSuperAdmin || viewer.Has(AppPermissions.CloseCorrespondence));
            dto.CanClose = incoming && open && handles && (viewer.IsSuperAdmin || viewer.Has(AppPermissions.CloseCorrespondence));
            dto.CanAttach = Allowed(() => DiwanRules.EnsureCanAttach(viewer, c));
            dto.CanSubmit = DiwanRules.IsOutgoingDraft(c) && dto.CanEdit && c.Attachments.Count > 0 && c.SubmittedAt == null;
            dto.CanApprove = Allowed(() => DiwanRules.EnsureCanDecideDraft(viewer, c, c.Attachments.Count, true, null));
            dto.CanIssue = Allowed(() => DiwanRules.EnsureCanIssue(viewer, c));
            return dto;
        }

        /// <summary>سطر «الحقل: قديم ← جديد» إن تغيّرت القيمة، وإلا null</summary>
        public static string? Change(string label, string? from, string? to)
        {
            from = (from ?? string.Empty).Trim();
            to = (to ?? string.Empty).Trim();
            if (string.Equals(from, to, StringComparison.Ordinal)) return null;
            string Show(string v) => v.Length == 0 ? "—" : v.Length > 80 ? v[..79] + "…" : v;
            return $"{label}: {Show(from)} ← {Show(to)}";
        }

        public static string Lines(params string?[] changes) => string.Join("\n", changes.Where(x => !string.IsNullOrEmpty(x)));

        /// <summary>عنوان مقروء للمعاملة في الأثر والإشعارات: «IN-2026-00012 — الموضوع» أو «مسودة — الموضوع»</summary>
        public static string Label(Correspondence c) =>
            $"{DiwanRules.Number(c.Direction, c.Year, c.Sequence) ?? "مسودة"} — {c.Subject}";
    }
}
