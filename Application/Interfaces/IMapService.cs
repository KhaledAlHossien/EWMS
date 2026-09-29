using Application.DTOs.Response;

namespace Application.Interfaces
{
    /// <summary>بيانات خريطة سوريا حسب الفرع (لا يتحقق من الصلاحيات — ذلك في GetBranchMapQuery)</summary>
    public interface IMapService
    {
        /// <returns>null إن لم يوجد الفرع</returns>
        Task<BranchMapDto?> GetBranchMapAsync(int branchId);

        /// <summary>كل الفروع مع علامة وجود بيانات خريطة لها</summary>
        Task<List<MapBranchOptionDto>> GetBranchOptionsAsync();
    }
}
