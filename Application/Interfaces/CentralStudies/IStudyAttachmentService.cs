using Domain.Entities.CentralStudiesBranch;

namespace Application.Interfaces
{
    public interface IStudyAttachmentService
    {
        /// <summary>مرفقات مالك واحد مع النوع ومن رفعها (بلا المحتوى)</summary>
        Task<List<StudyAttachment>> GetForOwnerAsync(StudyEntityType ownerType, int ownerId);
        Task<StudyAttachment?> GetByIdAsync(int id);
        Task<int> CountForOwnerAsync(StudyEntityType ownerType, int ownerId);
        /// <summary>مرفقات المالك المرفوعة بعد لحظة معيّنة (صورة كتاب الجولة الجديدة في معاملة الديوان)</summary>
        Task<int> CountForOwnerSinceAsync(StudyEntityType ownerType, int ownerId, DateTime since);
        /// <summary>السجل فقط — الملف نفسه في مجلد المرفقات (StorageKey)</summary>
        Task AddAsync(StudyAttachment attachment);
        Task DeleteAsync(StudyAttachment attachment);
    }
}
