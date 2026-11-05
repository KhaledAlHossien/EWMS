using Domain.Entities.CentralStudiesBranch;

namespace Application.Interfaces
{
    /// <summary>قوائم فرع الدراسات: أنواع المراحل، أنواع المرفقات، الاختصاصات الهندسية</summary>
    public interface IStudyLookupService
    {
        Task<List<StudyStageType>> GetStageTypesAsync(bool activeOnly);
        Task<StudyStageType?> GetStageTypeAsync(int id);
        Task<bool> StageTypeNameExistsAsync(string name, int? excludeId = null);
        /// <summary>حراسة الحذف: نوع مستخدم في مراحل مشاريع</summary>
        Task<bool> StageTypeInUseAsync(int id);

        Task<List<StudyAttachmentType>> GetAttachmentTypesAsync(bool activeOnly);
        Task<StudyAttachmentType?> GetAttachmentTypeAsync(int id);
        Task<bool> AttachmentTypeNameExistsAsync(string name, int? excludeId = null);
        Task<bool> AttachmentTypeInUseAsync(int id);

        Task<List<EngineeringDiscipline>> GetDisciplinesAsync(bool activeOnly);
        Task<EngineeringDiscipline?> GetDisciplineAsync(int id);
        Task<bool> DisciplineNameExistsAsync(string name, int? excludeId = null);
        Task<bool> DisciplineInUseAsync(int id);
        /// <summary>عدد الاختصاصات الفعّالة الموجودة من المعرّفات المعطاة</summary>
        Task<int> CountActiveDisciplinesAsync(IEnumerable<int> ids);

        Task AddAsync<T>(T entity) where T : class;
        Task RemoveAsync<T>(T entity) where T : class;
        Task SaveAsync();
    }
}
