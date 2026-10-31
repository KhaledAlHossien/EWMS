namespace Application.DTOs.Response
{
    /// <summary>سطر في قائمة طلبات التسعير</summary>
    public class PricingListItemDto
    {
        public int Id { get; set; }
        /// <summary>الرقم الداخلي PRC-…</summary>
        public string Number { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public int? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public int? ContractId { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool OutsideContractScope { get; set; }
        public int Status { get; set; }
        public string StatusAr { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
        /// <summary>تجاوز موعده وهو لم ينتهِ (مرشّح متابعة، لا قرار)</summary>
        public bool IsOverdue { get; set; }
        public int ItemsCount { get; set; }
        public int OffersCount { get; set; }
        public int? AssigneeUserId { get; set; }
        public string? AssigneeName { get; set; }
        public string RegisteredByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>سعر بند عند مورد في المقارنة: القابل للمقارنة وحدته كوحدة البند، و«الأرخص» معلومة لا اختيار</summary>
    public class PricingComparisonRowDto
    {
        public int OfferId { get; set; }
        public int LineId { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        /// <summary>سعر الوحدة × كمية البند (بعملة العرض)</summary>
        public decimal Total { get; set; }
        public int Conformity { get; set; }
        public string ConformityAr { get; set; } = string.Empty;
        public string ConformityNote { get; set; } = string.Empty;
        public bool Comparable { get; set; }
        public string? NotComparableReason { get; set; }
        /// <summary>الأرخص بين المقارَن المطابق فنياً لعملته (معلومة للعرض لا اختيار)</summary>
        public bool IsLowest { get; set; }
        public DateTime? ValidUntil { get; set; }
        public bool OfferExpired { get; set; }
        public bool IsSelected { get; set; }
        public string SelectionReason { get; set; } = string.Empty;
        /// <summary>يملك المستخدم اختيار هذا السطر الآن (بالقواعد نفسها)</summary>
        public bool CanSelect { get; set; }
        public bool CanUnselect { get; set; }
    }

    public class PricingItemDto
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Specifications { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        /// <summary>عملات أسعار البند (أكثر من واحدة: مجموعات منفصلة لا تُقارَن ولا تُجمع)</summary>
        public List<string> Currencies { get; set; } = [];
        public List<PricingComparisonRowDto> Rows { get; set; } = [];
        public bool HasSelection { get; set; }
    }

    public class PricingOfferLineDto
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public decimal UnitPrice { get; set; }
        public string Unit { get; set; } = string.Empty;
        public int Conformity { get; set; }
        public string ConformityNote { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }

    public class PricingOfferDto
    {
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? OfferNumber { get; set; }
        public DateTime ReceivedAt { get; set; }
        public DateTime? ValidUntil { get; set; }
        public bool Expired { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string SupplyTerms { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public int? DocumentVersionId { get; set; }
        public string RecordedByName { get; set; } = string.Empty;
        public List<PricingOfferLineDto> Lines { get; set; } = [];
        public bool CanEdit { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>مجموع المختار بعملة (جمع بسيط لسعر الوحدة × كمية البند؛ العملات لا تُجمع معاً)</summary>
    public class PricingTotalDto
    {
        public string Currency { get; set; } = string.Empty;
        public decimal Total { get; set; }
    }

    public class PricingDetailDto : PricingListItemDto
    {
        public string Description { get; set; } = string.Empty;
        public int? CorrespondenceId { get; set; }
        public string? ContractNumber { get; set; }
        public DateTime? AssignedAt { get; set; }
        public string? AssignedByName { get; set; }
        public string RecommendationNote { get; set; } = string.Empty;
        public string? RecommendedByName { get; set; }
        public DateTime? RecommendedAt { get; set; }
        public string DecisionNote { get; set; } = string.Empty;
        public string? DecidedByName { get; set; }
        public DateTime? DecidedAt { get; set; }
        public string? ResultRecipient { get; set; }
        public DateTime? ResultSentAt { get; set; }
        public string? ResultSentByName { get; set; }
        public string ResultNote { get; set; } = string.Empty;
        public List<PricingItemDto> Items { get; set; } = [];
        public List<PricingOfferDto> Offers { get; set; } = [];
        public List<PricingTotalDto> SelectedTotals { get; set; } = [];

        // أعلام الأزرار: بالقواعد نفسها التي يفرضها الخادم
        public bool CanEdit { get; set; }
        /// <summary>تغيير بنود الطلب (قبل أول عرض)</summary>
        public bool CanEditItems { get; set; }
        public bool CanAssign { get; set; }
        public bool CanCancel { get; set; }
        public bool CanAddOffer { get; set; }
        public bool CanRecommend { get; set; }
        public bool CanDecide { get; set; }
        public bool CanSendResult { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    public class PricingAgreementLineDto
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
    }

    public class PricingAgreementListItemDto
    {
        public int Id { get; set; }
        /// <summary>الرقم الداخلي AGR-…</summary>
        public string InternalNumber { get; set; } = string.Empty;
        public string? Number { get; set; }
        public int BranchId { get; set; }
        public DateTime AgreementDate { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public int? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string Currency { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public int Status { get; set; }
        public string StatusAr { get; set; } = string.Empty;
        public string PreparedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class PricingAgreementDetailDto : PricingAgreementListItemDto
    {
        public int? ContractId { get; set; }
        public int? PricingRequestId { get; set; }
        public string TechnicalSpecs { get; set; } = string.Empty;
        public string ExecutionMethod { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public int? DocumentVersionId { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public string? ApprovedByName { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string ApprovalNote { get; set; } = string.Empty;
        public string ReturnNote { get; set; } = string.Empty;
        public List<PricingAgreementLineDto> Lines { get; set; } = [];
        public bool CanEdit { get; set; }
        public bool CanSubmit { get; set; }
        public bool CanReview { get; set; }
        public bool CanCancel { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }
}
