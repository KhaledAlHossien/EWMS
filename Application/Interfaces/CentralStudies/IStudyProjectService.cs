using Application.DTOs.Request;
using Domain.Entities.CentralStudiesBranch;

namespace Application.Interfaces
{
    /// <summary>المشاريع وأقسامها المشاركة وفريقها</summary>
    public interface IStudyProjectService
    {
        /// <summary>
        /// مع أمر البدء (والرد والتكليف والطلب) والفرع والمدير والمراحل ومهامها؛
        /// withTeam: الأقسام المشاركة وأعضاؤها أيضاً
        /// </summary>
        Task<StudyProject?> GetByIdAsync(int id, bool withTeam = false);
        Task<(List<StudyProject> Items, int Total)> SearchAsync(StudyDocumentFilterDto filter, int? branchLimit, int page, int pageSize);
        Task<bool> ExistsForStartOrderAsync(int startOrderId);
        Task<bool> CodeExistsAsync(string code, int? excludeId = null);
        /// <summary>للمشروع مهام أو مخططات أو مرفقات (لا يُحذف)</summary>
        Task<bool> HasContentAsync(int projectId);
        /// <summary>رقم الطلب جذر الدراسة (لسجل الدراسة)</summary>
        Task<int> GetRequestIdAsync(int projectId);

        Task<StudyProject> AddAsync(StudyProject project);
        Task UpdateAsync(StudyProject project, byte[]? expectedRowVersion = null);
        Task DeleteAsync(StudyProject project);

        /// <summary>مشاركة قسم مع المشروع وأعضائها</summary>
        Task<ProjectDepartment?> GetProjectDepartmentAsync(int id);
        Task<bool> DepartmentParticipatesAsync(int projectId, int departmentId);
        /// <summary>مهام المشروع المسندة إلى القسم أو أحد مكاتبه</summary>
        Task<bool> HasTasksForDepartmentAsync(int projectId, int departmentId);
        Task<bool> IsMemberAsync(int projectId, int userId);
        /// <summary>للعضو مهام في المشروع (لا يُزال قبل نقلها)</summary>
        Task<bool> HasTasksForUserAsync(int projectId, int userId);
        Task AddProjectDepartmentAsync(ProjectDepartment projectDepartment);
        Task RemoveProjectDepartmentAsync(ProjectDepartment projectDepartment);
        Task AddMemberAsync(ProjectMember member);
        Task RemoveMemberAsync(ProjectMember member);

        Task SaveAsync();
    }
}
