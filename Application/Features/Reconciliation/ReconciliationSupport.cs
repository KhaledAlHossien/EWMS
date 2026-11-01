using Application.Common;
using Application.DTOs.Response;
using Domain.Entities;

namespace Application.Features.Reconciliation
{
    /// <summary>تحويل المطابقة إلى DTO بفروقها وأعلامها (يحسبها الخادم بقواعده)</summary>
    internal static class ReconciliationSupport
    {
        public static ReconciliationListItemDto ToListItem(PriceReconciliation r) => new()
        {
            Id = r.Id, Number = ReconciliationRules.Number(r.Id, r.CreatedAt), BranchId = r.BranchId, DocumentKind = (int)r.DocumentKind, DocumentKindAr = ReconciliationRules.KindAr(r.DocumentKind),
            DocumentNumber = r.DocumentNumber, DocumentDate = r.DocumentDate, SupplierId = r.SupplierId, SupplierName = r.Supplier.Name, ProjectId = r.ProjectId, ProjectName = r.Project?.Name,
            Currency = r.Currency, Total = ReconciliationRules.InvoiceTotal(r), LinesCount = r.Lines.Count, Status = (int)r.Status, StatusAr = ReconciliationRules.StatusAr(r.Status),
            PreparedByName = r.PreparedByUser.FullName, CreatedAt = r.CreatedAt
        };

        private static ReconciliationLineDto ToLine(PriceReconciliationLine l, string currency)
        {
            var c = ReconciliationRules.Compare(l, currency);
            return new ReconciliationLineDto
            {
                Id = l.Id, SortOrder = l.SortOrder, Description = l.Description, Unit = l.Unit, Quantity = l.InvoiceQuantity, UnitPrice = l.InvoiceUnitPrice, Total = l.InvoiceQuantity * l.InvoiceUnitPrice,
                ReferenceKind = (int)l.ReferenceKind, ReferenceOfferLineId = l.ReferenceOfferLineId, ReferencePriceId = l.ReferencePriceId, ReferenceLabel = l.ReferenceLabel,
                ReferenceQuantity = l.ReferenceQuantity, ReferenceUnitPrice = l.ReferenceUnitPrice, ReferenceUnit = l.ReferenceUnit, ReferenceCurrency = l.ReferenceCurrency,
                HasReference = c.HasReference, Comparable = c.Comparable, NotComparableReason = c.NotComparableReason, PriceDifference = c.PriceDifference, QuantityDifference = c.QuantityDifference,
                HasDifference = c.HasPriceDifference || c.HasQuantityDifference, Note = l.Note
            };
        }

        public static ReconciliationDetailDto ToDetail(PriceReconciliation r, Viewer viewer)
        {
            var suggested = ReconciliationRules.Suggest(r);
            return new ReconciliationDetailDto
            {
                Id = r.Id, Number = ReconciliationRules.Number(r.Id, r.CreatedAt), BranchId = r.BranchId, DocumentKind = (int)r.DocumentKind, DocumentKindAr = ReconciliationRules.KindAr(r.DocumentKind),
                DocumentNumber = r.DocumentNumber, DocumentDate = r.DocumentDate, SupplierId = r.SupplierId, SupplierName = r.Supplier.Name, ProjectId = r.ProjectId, ProjectName = r.Project?.Name,
                ContractId = r.ContractId, ContractNumber = r.Contract?.Number, Currency = r.Currency, Notes = r.Notes, DocumentVersionId = r.DocumentVersionId, Total = ReconciliationRules.InvoiceTotal(r),
                Status = (int)r.Status, StatusAr = ReconciliationRules.StatusAr(r.Status), AuditResult = (int?)r.AuditResult, AuditResultAr = r.AuditResult is ReconciliationStatus a ? ReconciliationRules.StatusAr(a) : null,
                AuditNote = r.AuditNote, PreparedByName = r.PreparedByUser.FullName, AuditedAt = r.AuditedAt, DecidedByName = r.DecidedByUser?.FullName, DecidedAt = r.DecidedAt, DecisionNote = r.DecisionNote,
                ReferredTo = r.ReferredTo, ReferralNote = r.ReferralNote, ReferredAt = r.ReferredAt, ReferredByName = r.ReferredByUser?.FullName, CreatedAt = r.CreatedAt,
                Lines = r.Lines.OrderBy(l => l.SortOrder).ThenBy(l => l.Id).Select(l => ToLine(l, r.Currency)).ToList(),
                SuggestedResult = (int)suggested, SuggestedResultAr = ReconciliationRules.StatusAr(suggested),
                CanEdit = Can(() => ReconciliationRules.EnsureCanEdit(viewer, r)), CanRecord = Can(() => ReconciliationRules.EnsureCanEdit(viewer, r)) && r.Lines.Count > 0,
                CanDecide = Can(() => ReconciliationRules.EnsureCanDecide(viewer, r, true, null)), CanRefer = Can(() => ReconciliationRules.EnsureCanRefer(viewer, r, "x")),
                CanCancel = Can(() => ReconciliationRules.EnsureCanCancel(viewer, r)), RowVersion = r.RowVersion
            };
        }

        private static bool Can(Action rule)
        {
            try { rule(); return true; }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException) { return false; }
        }
    }
}
