using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Requests.Queries.Search
{
    // طلب تجهيز دراسة تكاليف — جذر السلسلة. حدّ كل الصلاحيات: طلبات فرعه (والمؤسسة كلها لنظرة المؤسسة).
    // التسجيل النهائي (Submit) يجعله معتمداً مباشرة (لا تواقيع على الطلب)، فيُصدَر له تكليف.
    public record SearchStudyRequestsQuery(StudyDocumentFilterDto Filter) : IRequest<PagedResultDto<StudyRequestDto>>;
}
