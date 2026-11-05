using Application.DTOs.Request;
using Domain.Entities.CentralStudiesBranch;

namespace Application.Interfaces
{
    /// <summary>أوامر بدء الدراسة ومهندسوها ومواردها</summary>
    public interface IStudyStartOrderService
    {
        /// <summary>مع الرد والتكليف والطلب وفرعه والقسم المختص والمدير والمهندسين والموارد والمشروع</summary>
        Task<StudyStartOrder?> GetByIdAsync(int id);
        Task<(List<StudyStartOrder> Items, int Total)> SearchAsync(StudyDocumentFilterDto filter, int? branchLimit, int page, int pageSize);
        Task<bool> ExistsForResponseAsync(int responseId);

        Task<StudyStartOrder> AddAsync(StudyStartOrder order);
        Task UpdateAsync(StudyStartOrder order, byte[]? expectedRowVersion = null);
        Task DeleteAsync(StudyStartOrder order);
    }
}
