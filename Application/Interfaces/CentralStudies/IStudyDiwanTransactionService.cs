using Application.DTOs.Request;
using Domain.Entities.CentralStudiesBranch;

namespace Application.Interfaces
{
    /// <summary>معاملات الديوان (DWN) — مستند بين الرد المعتمد وأمر البدء</summary>
    public interface IStudyDiwanTransactionService
    {
        /// <summary>مع الرد وتكليفه وطلبه وفرعه وأمر بدء الرد والمنشئ</summary>
        Task<StudyDiwanTransaction?> GetByIdAsync(int id);
        Task<(List<StudyDiwanTransaction> Items, int Total)> SearchAsync(StudyDocumentFilterDto filter, int? branchLimit, int page, int pageSize);
        Task<bool> ExistsForResponseAsync(int responseId);
        Task<StudyDiwanTransaction> AddAsync(StudyDiwanTransaction transaction);
        Task UpdateAsync(StudyDiwanTransaction transaction, byte[]? expectedRowVersion = null);
        Task DeleteAsync(StudyDiwanTransaction transaction);
    }
}
