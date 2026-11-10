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
        public async Task<PagedResultDto<StudyActivityLogDto>> PageAsync(int requestId, int page, int pageSize,
            int? projectId = null, ProjectScope? scope = null)
        {
            var (p, size) = StudyRules.NormalizePaging(page, pageSize <= 0 ? 50 : pageSize);
            // لغير صاحب الرؤية الكاملة (2026-11-10): أحداث المشروع تُخفى وتبقى مهام يراها، والوثائق والمخططات كما هي
            var filter = scope is { IsFull: false } ? scope.TaskFilter() : null;
            var (items, total) = await _service.GetForRequestAsync(requestId, p, size, projectId, filter);
            return new PagedResultDto<StudyActivityLogDto> { Items = items, TotalCount = total, Page = p, PageSize = size };
        }
    }
}
