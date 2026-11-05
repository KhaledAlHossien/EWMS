using Domain.Entities.CentralStudiesBranch;

namespace Application.Interfaces
{
    /// <summary>سطور التوقيع على مستندات الدراسات</summary>
    public interface IStudyApprovalService
    {
        /// <summary>سطور مستند بترتيبها مع الموقّعين</summary>
        Task<List<StudyApproval>> GetForOwnerAsync(StudyEntityType ownerType, int ownerId);

        /// <summary>سطر مع مستنده كاملاً (السلسلة حتى الفرع، والقسم المعني بالصفة) — متتبَّع للتعديل</summary>
        Task<StudyApproval?> GetWithOwnerAsync(int id);

        /// <summary>السطور المعلّقة لصفات معيّنة مع مستنداتها (للتصفية حسب الحدود والترتيب)</summary>
        Task<List<StudyApproval>> GetPendingAsync(IReadOnlyCollection<StudySignerRole> roles);

        /// <summary>يستبدل سطور المستند بسطور جديدة (لا يحفظ)</summary>
        Task ReplaceForOwnerAsync(StudyEntityType ownerType, int ownerId, List<StudyApproval> rows);

        /// <summary>يحفظ كل التغييرات المتتبَّعة (السطر ومستنده معاً) — التعديل المتزامن رسالة واضحة</summary>
        Task SaveAsync();
    }
}
