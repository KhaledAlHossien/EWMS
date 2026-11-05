using Application.DTOs.Request;
using Domain.Entities.CentralStudiesBranch;

namespace Application.Interfaces
{
    public interface IStudyAssignmentService
    {
        /// <summary>مع الطلب وفرعه والقسم المتابع والمنشئ والرد</summary>
        Task<StudyAssignment?> GetByIdAsync(int id);
        Task<(List<StudyAssignment> Items, int Total)> SearchAsync(StudyDocumentFilterDto filter, int? branchLimit, int page, int pageSize);
        Task<bool> ExistsForRequestAsync(int requestId);
        /// <summary>كود المشروع مستخدم في تكليف آخر أو في مشروع</summary>
        Task<bool> ProjectCodeExistsAsync(string code, int? excludeAssignmentId = null);

        Task<StudyAssignment> AddAsync(StudyAssignment assignment);
        Task UpdateAsync(StudyAssignment assignment, byte[]? expectedRowVersion = null);
        Task DeleteAsync(StudyAssignment assignment);
    }
}
