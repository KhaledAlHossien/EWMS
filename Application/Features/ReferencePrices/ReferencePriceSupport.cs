using Application.Common;
using Application.DTOs.Response;
using Domain.Entities;

namespace Application.Features.ReferencePrices
{
    /// <summary>تحويل السعر المرجعي إلى DTO بأعلامه (يحسبها الخادم بقواعده)</summary>
    internal static class ReferencePriceSupport
    {
        public static ReferencePriceListItemDto ToListItem(ReferencePrice p, HashSet<int> latest, HashSet<int> latestForSupplier) => new()
        {
            Id = p.Id, Number = ReferencePriceRules.Number(p.Id, p.CreatedAt), BranchId = p.BranchId, ItemCode = p.ItemCode, ItemName = p.ItemName, Unit = p.Unit,
            SupplierId = p.SupplierId, SupplierName = p.Supplier.Name, UnitPrice = p.UnitPrice, Currency = p.Currency, PriceDate = p.PriceDate,
            ProjectId = p.ProjectId, ProjectName = p.Project?.Name, Source = (int)p.Source, SourceAr = ReferencePriceRules.SourceAr(p.Source), SourceDocument = p.SourceDocument,
            Status = (int)p.Status, StatusAr = ReferencePriceRules.StatusAr(p.Status), IsLatest = latest.Contains(p.Id), IsLatestForSupplier = latestForSupplier.Contains(p.Id),
            CreatedByName = p.CreatedByUser.FullName, ApprovedByName = p.ApprovedByUser?.FullName, SupersedesId = p.SupersedesPriceId
        };

        public static ReferencePriceDetailDto ToDetail(ReferencePrice p, Viewer viewer, ReferencePrice? successor, IReadOnlyCollection<ReferencePrice> group)
        {
            var (latest, latestForSupplier) = ReferencePriceRules.Latest(group);
            return new ReferencePriceDetailDto
            {
                Id = p.Id, Number = ReferencePriceRules.Number(p.Id, p.CreatedAt), BranchId = p.BranchId, ItemCode = p.ItemCode, ItemName = p.ItemName, Specifications = p.Specifications,
                Unit = p.Unit, SupplierId = p.SupplierId, SupplierName = p.Supplier.Name, UnitPrice = p.UnitPrice, Currency = p.Currency, PriceDate = p.PriceDate,
                ProjectId = p.ProjectId, ProjectName = p.Project?.Name, Source = (int)p.Source, SourceAr = ReferencePriceRules.SourceAr(p.Source), SourceDocument = p.SourceDocument,
                DocumentVersionId = p.DocumentVersionId, Notes = p.Notes, Status = (int)p.Status, StatusAr = ReferencePriceRules.StatusAr(p.Status),
                IsLatest = latest.Contains(p.Id), IsLatestForSupplier = latestForSupplier.Contains(p.Id),
                CreatedByName = p.CreatedByUser.FullName, CreatedAt = p.CreatedAt, ApprovedByName = p.ApprovedByUser?.FullName, ApprovedAt = p.ApprovedAt, ApprovalNote = p.ApprovalNote,
                SupersedesId = p.SupersedesPriceId, SupersedesNumber = p.SupersedesPrice is null ? null : ReferencePriceRules.Number(p.SupersedesPrice.Id, p.SupersedesPrice.CreatedAt),
                CorrectionReason = p.CorrectionReason,
                SupersededById = successor?.Id, SupersededByNumber = successor is null ? null : ReferencePriceRules.Number(successor.Id, successor.CreatedAt),
                History = group.OrderByDescending(x => x.PriceDate).ThenByDescending(x => x.Id).Select(x => ToListItem(x, latest, latestForSupplier)).ToList(),
                CanCorrect = CanCorrect(viewer, p, successor), CanApprove = CanApprove(viewer, p), RowVersion = p.RowVersion
            };
        }

        private static bool CanCorrect(Viewer viewer, ReferencePrice p, ReferencePrice? successor)
        {
            if (successor != null) return false;
            try { ReferencePriceRules.EnsureCanCorrect(viewer, p, "x"); return true; }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException) { return false; }
        }

        private static bool CanApprove(Viewer viewer, ReferencePrice p)
        {
            try { ReferencePriceRules.EnsureCanApprove(viewer, p, null); return true; }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException) { return false; }
        }
    }
}
