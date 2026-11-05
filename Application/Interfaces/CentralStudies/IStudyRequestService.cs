using Application.DTOs.Request;
using Application.DTOs.Response;
using Domain.Entities.CentralStudiesBranch;

namespace Application.Interfaces
{
    public interface IStudyRequestService
    {
        /// <summary>مع الفرع والموقع والمنشئ والتكليف</summary>
        Task<StudyRequest?> GetByIdAsync(int id);

        /// <summary>branchLimit: null = كل الفروع، 0 = لا شيء، وإلا فرع واحد</summary>
        Task<(List<StudyRequest> Items, int Total)> SearchAsync(StudyDocumentFilterDto filter, int? branchLimit, int page, int pageSize);

        Task<StudyRequest> AddAsync(StudyRequest request);
        /// <summary>expectedRowVersion: نسخة السجل التي رآها المستخدم (null = بلا فحص)</summary>
        Task UpdateAsync(StudyRequest request, byte[]? expectedRowVersion = null);
        Task DeleteAsync(StudyRequest request);

        /// <summary>سلسلة الدراسة كاملة من الطلب إلى المشروع</summary>
        Task<StudyChainDto> GetChainAsync(int requestId);
    }
}
