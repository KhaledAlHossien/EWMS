using Domain.Entities;

namespace Application.Interfaces
{
    /// <summary>فلاتر قائمة العقود (كلها اختيارية، تُجمع بـ AND)</summary>
    public sealed record ContractFilter(int? ProjectId, int? BranchId, ContractStatus? Status, ContractingMethod? Method, int? ContractorId, string? Search,
        bool WarrantyEndingOnly, DateTime Today, int WarrantyWindowDays);

    /// <summary>
    /// حفظ العقود والمقاولين ومعايير التقييم وما يتفرع عن العقد (عروض، ملاحق، لجان، محتجزات، تقييمات). كل كتابة تأخذ <see cref="AuditEntry"/> وتحفظه في
    /// معاملتها نفسها (القاعدة 6)، وتغيّر السجل بعد فتح المستخدم له يرفض الحفظ برسالة واضحة (rowVersion).
    /// </summary>
    public interface IContractService
    {
        // ───────── المقاولون (بيانات الفرع) ─────────
        Task<List<Contractor>> GetContractorsAsync(int? branchId, bool includeInactive);
        Task<Contractor?> GetContractorAsync(int id);
        Task<Contractor> AddContractorAsync(Contractor contractor, AuditEntry audit);
        Task UpdateContractorAsync(Contractor contractor, byte[]? rowVersion, AuditEntry audit);
        Task DeleteContractorAsync(Contractor contractor, AuditEntry audit);
        Task<bool> ContractorNameExistsAsync(int branchId, string name, int? excludeId = null);
        /// <summary>مرتبط بعقد أو عرض أو تقييم</summary>
        Task<bool> ContractorInUseAsync(int id);

        /// <summary>تقييمات المقاول المراجَعة على عقود مشاريع يراها المستخدم (ملف المقاول)</summary>
        Task<List<ContractorEvaluation>> GetContractorEvaluationsAsync(int contractorId, System.Linq.Expressions.Expression<Func<Project, bool>> visibleProjects);

        // ───────── معايير التقييم (بيانات الفرع) ─────────
        Task<List<EvaluationCriterion>> GetCriteriaAsync(int? branchId, bool includeInactive);
        Task<EvaluationCriterion?> GetCriterionAsync(int id);
        Task<Dictionary<int, EvaluationCriterion>> GetCriteriaByIdsAsync(IReadOnlyCollection<int> ids);
        Task<EvaluationCriterion> AddCriterionAsync(EvaluationCriterion criterion, AuditEntry audit);
        Task UpdateCriterionAsync(EvaluationCriterion criterion, byte[]? rowVersion, AuditEntry audit);
        Task DeleteCriterionAsync(EvaluationCriterion criterion, AuditEntry audit);
        Task<bool> CriterionNameExistsAsync(int branchId, string name, int? excludeId = null);
        /// <summary>عليه درجات في تقييمات</summary>
        Task<bool> CriterionInUseAsync(int id);

        // ───────── العقود ─────────
        /// <summary>العقد بمشروعه ومشاركيه وعروضه وملاحقه ولجانه ومحتجزاته وتقييماته: ما تحتاجه القواعد في الذاكرة</summary>
        Task<Contract?> GetByIdAsync(int id);
        Task<Contract?> GetByOfferAsync(int offerId);
        Task<Contract?> GetByCommitteeAsync(int committeeId);
        Task<Contract?> GetByRetentionEntryAsync(int entryId);
        Task<Contract?> GetByEvaluationAsync(int evaluationId);

        Task<(List<Contract> Items, int TotalCount)> SearchAsync(
            System.Linq.Expressions.Expression<Func<Project, bool>> visibleProjects, ContractFilter filter, int page, int pageSize);

        Task<bool> NumberExistsAsync(int branchId, string number, int? excludeContractId = null);

        Task<Contract> AddAsync(Contract contract, AuditEntry audit);

        /// <summary>يحفظ تغييرات العقد المحمَّل (بيانات، حالة، إرساء…) وأثرها معاً. rowVersion = ما رآه المستخدم</summary>
        Task UpdateAsync(Contract contract, byte[]? rowVersion, AuditEntry audit);

        Task AddOfferAsync(Contract contract, ContractOffer offer, AuditEntry audit);
        Task AddAmendmentAsync(Contract contract, ContractAmendment amendment, AuditEntry audit);
        Task AddRetentionAsync(Contract contract, RetentionEntry entry, AuditEntry audit);
        Task AddRetentionDecisionAsync(Contract contract, RetentionDecision decision, AuditEntry audit);

        /// <summary>
        /// حفظ لجنة جديدة (أعضاؤها في <c>Members</c>) أو معدَّلة: الأعضاء المستبعدون <paramref name="removed"/> يُحذفون أولاً ثم يُضاف
        /// <paramref name="added"/> في خطوة ثانية (فلا يتعارض رئيس قديم مع جديد في الفهرس الفريد)، والأثر في المعاملة نفسها.
        /// </summary>
        Task SaveCommitteeAsync(Contract contract, Committee committee, bool isNew, IReadOnlyCollection<CommitteeMember> added, IReadOnlyCollection<CommitteeMember> removed, byte[]? rowVersion, AuditEntry audit);

        // ───────── تقييم المقاول ─────────
        Task AddEvaluationAsync(Contract contract, ContractorEvaluation evaluation, AuditEntry audit);
        /// <summary>حفظ التقييم المحمَّل (درجات مضافة أو معدَّلة، حالة…) مع حذف الدرجات المستبعدة صراحةً</summary>
        Task SaveEvaluationAsync(Contract contract, ContractorEvaluation evaluation, IReadOnlyCollection<EvaluationScore> removed, byte[]? rowVersion, AuditEntry audit);
        Task DeleteEvaluationAsync(Contract contract, ContractorEvaluation evaluation, AuditEntry audit);

        // ───────── الوثائق ─────────
        /// <summary>إصدار من إضبارة المشروع (لتثبيته في ملحق أو قرار أو تقييم)</summary>
        Task<ProjectDocumentVersion?> GetVersionAsync(int versionId);
    }
}
