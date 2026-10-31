using Application.Common;
using Application.DTOs.Response;
using Domain.Entities;

namespace Application.Features.Contracts
{
    /// <summary>
    /// العقود: ربط الكيانات بالـ DTOs وحساب أعلام الأزرار بالقواعد نفسها التي يفرضها الخادم
    /// (فلا تتكرر القواعد في الواجهة، وما يظهر زراً هو ما ينجح).
    /// </summary>
    public static class ContractSupport
    {
        public static ContractorResponseDto ToDto(Contractor c) => new()
        {
            Id = c.Id, BranchId = c.BranchId, Name = c.Name, RegistrationNumber = c.RegistrationNumber, ContactName = c.ContactName, Phone = c.Phone,
            Address = c.Address, Notes = c.Notes, IsActive = c.IsActive, RowVersion = c.RowVersion
        };

        public static EvaluationCriterionResponseDto ToDto(EvaluationCriterion c) => new()
        {
            Id = c.Id, BranchId = c.BranchId, Name = c.Name, Description = c.Description, Weight = c.Weight, SortOrder = c.SortOrder,
            IsActive = c.IsActive, RowVersion = c.RowVersion
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

        public static string Money(decimal? value, string currency) => value is decimal v ? $"{v:0.##} {currency}" : "—";

        private static string Day(DateTime? d) => d?.ToString("yyyy-MM-dd") ?? string.Empty;

        /// <summary>هل تنجح هذه الحراسة للمستخدم؟</summary>
        private static bool Allowed(Action guard)
        {
            try { guard(); return true; }
            catch (Exception e) when (e is UnauthorizedAccessException or InvalidOperationException) { return false; }
        }

        /// <summary>
        /// من يرى التقييم: صاحب «عرض التقييمات» (ومدير النظام) يرى كل تقييمات العقود التي يراها؛ ومن يعدّ يرى ما أعدّه هو؛
        /// ومن يراجع يرى المرسَل والمراجَع (لا مسودات غيره).
        /// </summary>
        public static bool CanSeeEvaluation(Viewer viewer, ContractorEvaluation e) =>
            viewer.IsSuperAdmin || viewer.Has(AppPermissions.ViewContractorEvaluations)
            || e.EvaluatedByUserId == viewer.Id
            || (viewer.Has(AppPermissions.ReviewContractorEvaluation) && e.Status != EvaluationStatus.Draft);

        public static ContractListItemDto ToListItem(Contract c, DateTime today) => ToListItem<ContractListItemDto>(c, today);

        private static T ToListItem<T>(Contract c, DateTime today) where T : ContractListItemDto, new() => new()
        {
            Id = c.Id,
            Number = ContractRules.Number(c.Id, c.CreatedAt),
            OfficialNumber = c.Number,
            ProjectId = c.ProjectId,
            ProjectName = c.Project?.Name ?? string.Empty,
            BranchId = c.BranchId,
            Method = (int)c.Method,
            MethodAr = ContractRules.MethodAr(c.Method),
            Status = (int)c.Status,
            StatusAr = ContractRules.StatusAr(c.Status),
            ContractorId = c.ContractorId,
            ContractorName = c.Contractor?.Name,
            ContractDate = c.ContractDate,
            Value = c.Value,
            EffectiveValue = ContractRules.EffectiveValue(c),
            Currency = c.Currency,
            WarrantyEnd = c.WarrantyEnd,
            WarrantyDaysLeft = ContractRules.WarrantyDaysLeft(c, today),
            WarrantyEndingSoon = ContractRules.IsWarrantyEndingSoon(c, today, ContractContext.WarrantyWindowDays),
            CreatedAt = c.CreatedAt
        };

        public static ContractDetailDto ToDetail(Contract c, Viewer viewer, DateTime today)
        {
            var dto = ToListItem<ContractDetailDto>(c, today);
            dto.CorrespondenceId = c.CorrespondenceId;
            dto.CorrespondenceNumber = c.Correspondence is null ? null : Diwan.DiwanRules.Number(c.Correspondence.Direction, c.Correspondence.Year, c.Correspondence.Sequence);
            dto.ExecutionOrderNumber = c.ExecutionOrderNumber;
            dto.ExecutionOrderDate = c.ExecutionOrderDate;
            dto.StartDate = c.StartDate;
            dto.DurationDays = c.DurationDays;
            dto.EffectiveDurationDays = ContractRules.EffectiveDurationDays(c);
            dto.Scope = c.Scope;
            dto.Notes = c.Notes;
            dto.TenderNumber = c.TenderNumber;
            dto.TenderSentAt = c.TenderSentAt;
            dto.TenderOpenedAt = c.TenderOpenedAt;
            dto.AwardedAt = c.AwardedAt;
            dto.AwardNote = c.AwardNote;
            dto.WarrantyMonths = c.WarrantyMonths;
            dto.WarrantyStart = c.WarrantyStart;
            dto.SuggestedWarrantyEnd = ContractRules.SuggestedWarrantyEnd(c.WarrantyStart, c.WarrantyMonths);
            dto.StillWithheld = ContractRules.StillWithheld(c);
            dto.CreatedByName = c.CreatedByUser?.FullName ?? string.Empty;
            dto.RowVersion = c.RowVersion;

            dto.CanEdit = Allowed(() => ContractRules.EnsureCanEdit(viewer, c));
            dto.AllowedStatuses = new[] { ContractStatus.Active, ContractStatus.Completed, ContractStatus.Terminated, ContractStatus.Cancelled }
                .Where(s => Allowed(() => ContractRules.EnsureCanChangeStatus(viewer, c, s, "x"))).Select(s => (int)s).ToList();
            dto.CanAddOffer = dto.CanEdit && c.Method != ContractingMethod.Direct && c.Status == ContractStatus.Contracting
                && !c.Offers.Any(o => o.IsAwarded) && c.Offers.Count < ContractRules.MaxOffers;
            dto.CanAddAmendment = Allowed(() => ContractRules.EnsureCanAddAmendment(viewer, c, "x", "x", null, null, today, today));
            dto.CanManageCommittee = Allowed(() => ContractRules.EnsureCanManageCommittee(viewer, c));
            dto.CanRecordRetention = Allowed(() => ContractRules.EnsureCanRecordRetention(viewer, c, 1m, today, null, today));
            dto.CanEvaluate = Allowed(() => ContractRules.EnsureCanEvaluate(viewer, c));

            dto.Offers = c.Offers.OrderBy(o => o.Amount).ThenBy(o => o.Id).Select(o => new ContractOfferDto
            {
                Id = o.Id, ContractorId = o.ContractorId, ContractorName = o.Contractor?.Name ?? string.Empty, Amount = o.Amount, ReceivedAt = o.ReceivedAt,
                Note = o.Note, IsAwarded = o.IsAwarded, RecordedByName = o.RecordedByUser?.FullName ?? string.Empty,
                CanAward = Allowed(() => ContractRules.EnsureCanAward(viewer, c, o))
            }).ToList();

            dto.Amendments = c.Amendments.OrderBy(a => a.Number).Select(a => new ContractAmendmentDto
            {
                Id = a.Id, Number = a.Number, Title = a.Title, Description = a.Description, ValueChange = a.ValueChange, DurationChangeDays = a.DurationChangeDays,
                ApprovedAt = a.ApprovedAt, DocumentVersionId = a.DocumentVersionId, RecordedByName = a.RecordedByUser?.FullName ?? string.Empty, RecordedAt = a.RecordedAt
            }).ToList();

            dto.Committees = c.Committees.OrderBy(x => x.FormedAt).ThenBy(x => x.Id).Select(x => ToCommittee(x, dto.CanManageCommittee)).ToList();

            dto.Retention = c.RetentionEntries.OrderBy(e => e.WithheldAt).ThenBy(e => e.Id).Select(e =>
            {
                var state = ContractRules.StateOf(e);
                return new RetentionEntryDto
                {
                    Id = e.Id, Amount = e.Amount, WithheldAt = e.WithheldAt, Note = e.Note, State = (int)state, StateAr = ContractRules.RetentionStateAr(state),
                    RecordedByName = e.RecordedByUser?.FullName ?? string.Empty,
                    CanDecide = Allowed(() => ContractRules.EnsureCanDecideRetention(viewer, c, e, RetentionDecisionKind.Continue, today, "x", null, today)),
                    Decisions = e.Decisions.OrderBy(d => d.DecidedAt).ThenBy(d => d.Id).Select(d => new RetentionDecisionDto
                    {
                        Id = d.Id, Decision = (int)d.Decision, DecisionAr = ContractRules.RetentionDecisionAr(d.Decision), DecidedAt = d.DecidedAt,
                        ApprovingAuthority = d.ApprovingAuthority, Note = d.Note, DocumentVersionId = d.DocumentVersionId, RecordedByName = d.RecordedByUser?.FullName ?? string.Empty
                    }).ToList()
                };
            }).ToList();

            dto.Evaluations = c.Evaluations.Where(e => CanSeeEvaluation(viewer, e)).OrderByDescending(e => e.EvaluationDate).ThenByDescending(e => e.Id)
                .Select(e => ToEvaluation(c, e, viewer)).ToList();
            return dto;
        }

        /// <summary>لجنة (فنية ومالية لعقد، أو لجنة استلام) بأعضائها</summary>
        public static CommitteeDto ToCommittee(Committee x, bool canEdit) => new()
        {
            Id = x.Id, Kind = (int)x.Kind, KindAr = ContractRules.CommitteeKindAr(x.Kind), FormedAt = x.FormedAt, DecisionNumber = x.DecisionNumber,
            DecisionVersionId = x.DecisionVersionId, Recommendations = x.Recommendations, CanEdit = canEdit, RowVersion = x.RowVersion,
            Members = x.Members.OrderByDescending(m => m.IsChair).ThenBy(m => m.Id).Select(m => new CommitteeMemberDto
            {
                UserId = m.UserId, Name = m.User?.FullName ?? m.ExternalName ?? string.Empty, IsExternal = m.UserId == null, Capacity = m.Capacity, IsChair = m.IsChair
            }).ToList()
        };

        public static ContractEvaluationDto ToEvaluation(Contract c, ContractorEvaluation e, Viewer viewer)
        {
            var canEvaluate = Allowed(() => ContractRules.EnsureCanEvaluate(viewer, c));
            return new ContractEvaluationDto
            {
                Id = e.Id, ContractorId = e.ContractorId, ContractorName = e.Contractor?.Name ?? string.Empty,
                EvaluatedByUserId = e.EvaluatedByUserId, EvaluatedByName = e.EvaluatedByUser?.FullName ?? string.Empty, EvaluationDate = e.EvaluationDate,
                Status = (int)e.Status, StatusAr = ContractRules.EvaluationStatusAr(e.Status), Notes = e.Notes, DocumentVersionId = e.DocumentVersionId,
                SubmittedAt = e.SubmittedAt, ReviewedByName = e.ReviewedByUser?.FullName, ReviewedAt = e.ReviewedAt, ReviewNote = e.ReviewNote,
                Scores = e.Scores.OrderBy(s => s.Criterion?.SortOrder ?? 0).ThenBy(s => s.Id).Select(s => new EvaluationScoreDto
                {
                    CriterionId = s.CriterionId, CriterionName = s.CriterionName, Weight = s.Weight, Score = s.Score, Note = s.Note
                }).ToList(),
                CanEdit = canEvaluate && Allowed(() => ContractRules.EnsureCanEditEvaluation(viewer, e)),
                CanSubmit = Allowed(() => ContractRules.EnsureCanSubmitEvaluation(viewer, c, e)),
                CanReview = Allowed(() => ContractRules.EnsureCanReviewEvaluation(viewer, c, e, null)),
                RowVersion = e.RowVersion
            };
        }

        public static ContractorEvaluationHistoryDto ToHistory(ContractorEvaluation e, Viewer viewer) => new()
        {
            Id = e.Id, ContractorId = e.ContractorId, ContractorName = e.Contractor?.Name ?? string.Empty,
            EvaluatedByUserId = e.EvaluatedByUserId, EvaluatedByName = e.EvaluatedByUser?.FullName ?? string.Empty, EvaluationDate = e.EvaluationDate,
            Status = (int)e.Status, StatusAr = ContractRules.EvaluationStatusAr(e.Status), Notes = e.Notes, DocumentVersionId = e.DocumentVersionId,
            SubmittedAt = e.SubmittedAt, ReviewedByName = e.ReviewedByUser?.FullName, ReviewedAt = e.ReviewedAt, ReviewNote = e.ReviewNote,
            Scores = e.Scores.OrderBy(s => s.Criterion?.SortOrder ?? 0).ThenBy(s => s.Id).Select(s => new EvaluationScoreDto
            {
                CriterionId = s.CriterionId, CriterionName = s.CriterionName, Weight = s.Weight, Score = s.Score, Note = s.Note
            }).ToList(),
            RowVersion = e.RowVersion,
            ContractId = e.ContractId, ContractNumber = ContractRules.Number(e.ContractId, e.Contract.CreatedAt),
            ProjectId = e.Contract.ProjectId, ProjectName = e.Contract.Project?.Name ?? string.Empty
        };
    }
}
