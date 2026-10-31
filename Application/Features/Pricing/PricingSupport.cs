using Application.Common;
using Application.DTOs.Response;
using Application.Features.Contracts;
using Domain.Entities;

namespace Application.Features.Pricing
{
    /// <summary>
    /// التسعير: ربط الكيانات بالـ DTOs وحساب أعلام الأزرار بالقواعد نفسها التي يفرضها الخادم
    /// (فلا تتكرر القواعد في الواجهة، وما يظهر زراً هو ما ينجح).
    /// </summary>
    public static class PricingSupport
    {
        public static string? Change(string label, string? from, string? to) => ContractSupport.Change(label, from, to);
        public static string Lines(params string?[] changes) => ContractSupport.Lines(changes);

        private static bool Allowed(Action guard)
        {
            try { guard(); return true; }
            catch (Exception e) when (e is UnauthorizedAccessException or InvalidOperationException) { return false; }
        }

        private static T ToListItem<T>(PricingRequest r, DateTime today) where T : PricingListItemDto, new() => new()
        {
            Id = r.Id,
            Number = PricingRules.Number(r.Id, r.CreatedAt),
            BranchId = r.BranchId,
            ProjectId = r.ProjectId,
            ProjectName = r.Project?.Name,
            ContractId = r.ContractId,
            Title = r.Title,
            OutsideContractScope = r.OutsideContractScope,
            Status = (int)r.Status,
            StatusAr = PricingRules.StatusAr(r.Status),
            DueDate = r.DueDate,
            IsOverdue = PricingRules.IsOverdueCandidate(r, today),
            ItemsCount = r.Items.Count,
            OffersCount = r.Offers.Count,
            AssigneeUserId = r.AssigneeUserId,
            AssigneeName = r.Assignee?.FullName,
            RegisteredByName = r.RegisteredByUser?.FullName ?? string.Empty,
            CreatedAt = r.CreatedAt
        };

        public static PricingListItemDto ToListItem(PricingRequest r, DateTime today) => ToListItem<PricingListItemDto>(r, today);

        public static PricingDetailDto ToDetail(PricingRequest r, Viewer viewer, DateTime today)
        {
            var dto = ToListItem<PricingDetailDto>(r, today);
            dto.Description = r.Description;
            dto.CorrespondenceId = r.CorrespondenceId;
            dto.ContractNumber = r.Contract is null ? null : ContractRules.Number(r.Contract.Id, r.Contract.CreatedAt);
            dto.AssignedAt = r.AssignedAt;
            dto.AssignedByName = r.AssignedByUser?.FullName;
            dto.RecommendationNote = r.RecommendationNote;
            dto.RecommendedByName = r.RecommendedByUser?.FullName;
            dto.RecommendedAt = r.RecommendedAt;
            dto.DecisionNote = r.DecisionNote;
            dto.DecidedByName = r.DecidedByUser?.FullName;
            dto.DecidedAt = r.DecidedAt;
            dto.ResultRecipient = r.ResultRecipient;
            dto.ResultSentAt = r.ResultSentAt;
            dto.ResultSentByName = r.ResultSentByUser?.FullName;
            dto.ResultNote = r.ResultNote;
            dto.RowVersion = r.RowVersion;

            // مستخدم وهمي نشط من فرع الطلب لقياس «هل تنجح الحراسة» بلا اختيار فعلي
            var probeUser = new User { Id = -1, FullName = "-", Email = "-", PasswordHash = "-", RoleId = 0, IsActive = true, BranchId = r.BranchId };
            var probeSupplier = new Contractor { Id = -1, BranchId = r.BranchId, IsActive = true };
            dto.CanEdit = Allowed(() => PricingRules.EnsureCanEdit(viewer, r));
            dto.CanEditItems = dto.CanEdit && r.Offers.Count == 0;
            dto.CanAssign = Allowed(() => PricingRules.EnsureCanAssign(viewer, r, probeUser, true));
            dto.CanCancel = Allowed(() => PricingRules.EnsureCanCancel(viewer, r));
            dto.CanAddOffer = Allowed(() => PricingRules.EnsureCanAddOffer(viewer, r, probeSupplier));
            dto.CanRecommend = Allowed(() => PricingRules.EnsureCanRecommend(viewer, r, "x"));
            dto.CanDecide = Allowed(() => PricingRules.EnsureCanDecide(viewer, r, PricingRules.Decision.Approve, null));
            dto.CanSendResult = Allowed(() => PricingRules.EnsureCanSendResult(viewer, r, "x", null));
            var canWork = Allowed(() => PricingRules.EnsureCanWork(viewer, r));

            dto.Items = r.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => ToItem(r, i, viewer, today)).ToList();
            dto.Offers = r.Offers.OrderBy(o => o.ReceivedAt).ThenBy(o => o.Id).Select(o => new PricingOfferDto
            {
                Id = o.Id, SupplierId = o.SupplierId, SupplierName = o.Supplier?.Name ?? string.Empty, OfferNumber = o.OfferNumber, ReceivedAt = o.ReceivedAt,
                ValidUntil = o.ValidUntil, Expired = o.ValidUntil is DateTime v && v.Date < today.Date, Currency = o.Currency, SupplyTerms = o.SupplyTerms, Note = o.Note,
                DocumentVersionId = o.DocumentVersionId, RecordedByName = o.RecordedByUser?.FullName ?? string.Empty, CanEdit = canWork, RowVersion = o.RowVersion,
                Lines = o.Lines.OrderBy(l => l.PricingRequestItemId).Select(l => new PricingOfferLineDto
                {
                    Id = l.Id, ItemId = l.PricingRequestItemId, UnitPrice = l.UnitPrice, Unit = l.Unit, Conformity = (int)l.Conformity, ConformityNote = l.ConformityNote, IsSelected = l.IsSelected
                }).ToList()
            }).ToList();
            dto.SelectedTotals = PricingRules.SelectedTotals(r).OrderBy(x => x.Key).Select(x => new PricingTotalDto { Currency = x.Key, Total = x.Value }).ToList();
            return dto;
        }

        private static PricingItemDto ToItem(PricingRequest r, PricingRequestItem item, Viewer viewer, DateTime today)
        {
            var rows = PricingRules.Compare(item, r.Offers);
            return new PricingItemDto
            {
                Id = item.Id, SortOrder = item.SortOrder, Description = item.Description, Specifications = item.Specifications, Quantity = item.Quantity, Unit = item.Unit,
                Currencies = PricingRules.CurrenciesIn(rows),
                HasSelection = rows.Any(x => x.Line.IsSelected),
                Rows = rows.Select(x => new PricingComparisonRowDto
                {
                    OfferId = x.Offer.Id, LineId = x.Line.Id, SupplierId = x.Offer.SupplierId, SupplierName = x.Offer.Supplier?.Name ?? string.Empty,
                    UnitPrice = x.Line.UnitPrice, Unit = x.Line.Unit, Currency = x.Offer.Currency, Total = x.Total,
                    Conformity = (int)x.Line.Conformity, ConformityAr = PricingRules.ConformityAr(x.Line.Conformity), ConformityNote = x.Line.ConformityNote,
                    Comparable = x.Comparable, NotComparableReason = x.NotComparableReason, IsLowest = x.IsLowest,
                    ValidUntil = x.Offer.ValidUntil, OfferExpired = x.Offer.ValidUntil is DateTime v && v.Date < today.Date,
                    IsSelected = x.Line.IsSelected, SelectionReason = x.Line.SelectionReason,
                    CanSelect = !x.Line.IsSelected && Allowed(() => PricingRules.EnsureCanSelect(viewer, r, item, x.Offer, x.Line, "x", today)),
                    CanUnselect = Allowed(() => PricingRules.EnsureCanUnselect(viewer, r, x.Line))
                }).ToList()
            };
        }

        // ───────── محاضر الاتفاق ─────────

        private static T ToAgreementItem<T>(PricingAgreement a) where T : PricingAgreementListItemDto, new() => new()
        {
            Id = a.Id,
            InternalNumber = PricingRules.AgreementNumber(a.Id, a.CreatedAt),
            Number = a.Number,
            BranchId = a.BranchId,
            AgreementDate = a.AgreementDate,
            SupplierId = a.SupplierId,
            SupplierName = a.Supplier?.Name ?? string.Empty,
            ProjectId = a.ProjectId,
            ProjectName = a.Project?.Name,
            Currency = a.Currency,
            Total = PricingRules.AgreementTotal(a),
            Status = (int)a.Status,
            StatusAr = PricingRules.AgreementStatusAr(a.Status),
            PreparedByName = a.PreparedByUser?.FullName ?? string.Empty,
            CreatedAt = a.CreatedAt
        };

        public static PricingAgreementListItemDto ToAgreementListItem(PricingAgreement a) => ToAgreementItem<PricingAgreementListItemDto>(a);

        public static PricingAgreementDetailDto ToAgreementDetail(PricingAgreement a, Viewer viewer)
        {
            var dto = ToAgreementItem<PricingAgreementDetailDto>(a);
            dto.ContractId = a.ContractId;
            dto.PricingRequestId = a.PricingRequestId;
            dto.TechnicalSpecs = a.TechnicalSpecs;
            dto.ExecutionMethod = a.ExecutionMethod;
            dto.Notes = a.Notes;
            dto.DocumentVersionId = a.DocumentVersionId;
            dto.SubmittedAt = a.SubmittedAt;
            dto.ApprovedByName = a.ApprovedByUser?.FullName;
            dto.ApprovedAt = a.ApprovedAt;
            dto.ApprovalNote = a.ApprovalNote;
            dto.ReturnNote = a.ReturnNote;
            dto.RowVersion = a.RowVersion;
            dto.CanEdit = Allowed(() => PricingRules.EnsureCanEditAgreement(viewer, a));
            dto.CanSubmit = Allowed(() => PricingRules.EnsureCanSubmitAgreement(viewer, a));
            dto.CanReview = Allowed(() => PricingRules.EnsureCanApproveAgreement(viewer, a, true, null));
            dto.CanCancel = Allowed(() => PricingRules.EnsureCanCancelAgreement(viewer, a));
            dto.Lines = a.Lines.OrderBy(l => l.SortOrder).ThenBy(l => l.Id).Select(l => new PricingAgreementLineDto
            {
                Id = l.Id, SortOrder = l.SortOrder, Description = l.Description, Quantity = l.Quantity, Unit = l.Unit, UnitPrice = l.UnitPrice, Total = l.Quantity * l.UnitPrice
            }).ToList();
            return dto;
        }
    }
}
