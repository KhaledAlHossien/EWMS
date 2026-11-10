using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.ActivityLogs.Queries.GetByRequest
{
    // سجل الدراسة: كل ما حدث من الطلب حتى إغلاق المشروع (الأحدث أولاً). ViewStudyActivityLog — دراسات فرعه.
    // مرقَّم (page، pageSize ≤ 100): السجل يكبر مع الزمن ولا ينقص.
    public record GetStudyActivityLogQuery(int RequestId, int Page, int PageSize) : IRequest<PagedResultDto<StudyActivityLogDto>>;
}
