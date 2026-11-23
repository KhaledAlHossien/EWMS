using Application.DTOs.Request;
using Domain.Entities;
using Domain.Entities.CentralStudiesBranch;

namespace Application.Interfaces
{
    /// <summary>إشعارات الرد على التكليف وأجزاؤها</summary>
    public interface IStudyResponseService
    {
        /// <summary>مع التكليف وطلبه وفرعه والقسم المتابع والأجزاء وأمر البدء</summary>
        Task<StudyAssignmentResponse?> GetByIdAsync(int id);
        Task<StudyAssignmentResponse?> GetByDiwanReplyAsync(int replyId);
        Task<(List<StudyAssignmentResponse> Items, int Total)> SearchAsync(StudyDocumentFilterDto filter, int? branchLimit, int page, int pageSize);
        Task<bool> ExistsForAssignmentAsync(int assignmentId);

        Task<StudyAssignmentResponse> AddAsync(StudyAssignmentResponse response);
        Task UpdateAsync(StudyAssignmentResponse response, byte[]? expectedRowVersion = null);
        Task SaveDiwanStepAsync(StudyAssignmentResponse response, byte[] expectedRowVersion, StudyActivityLog activity,
            IReadOnlyCollection<Notification> notifications);
        Task DeleteAsync(StudyAssignmentResponse response);
    }
}
