using Application.DTOs.Response;
using Application.Interfaces;

namespace Application.Features.CentralStudies.ActivityLogs
{
    /// <summary>الدوال المشتركة بين عمليات ActivityLogs (تحميل السجل، التحويل إلى DTO، الأعلام…) — منقولة من المعالج الواحد القديم</summary>
    public class StudyActivityLogSupport
    {
        private readonly IStudyActivityLogService _service;

        public StudyActivityLogSupport(IStudyActivityLogService service)
        {
            _service = service;
        }

        // الافتراضي 50 حدثاً في الصفحة (الأقصى 100)
        public async Task<PagedResultDto<StudyActivityLogDto>> PageAsync(int requestId, int page, int pageSize)
        {
            var (p, size) = StudyRules.NormalizePaging(page, pageSize <= 0 ? 50 : pageSize);
            var (items, total) = await _service.GetForRequestAsync(requestId, p, size);
            return new PagedResultDto<StudyActivityLogDto> { Items = items, TotalCount = total, Page = p, PageSize = size };
        }
    }
}
