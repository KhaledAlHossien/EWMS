using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.StartOrders.Queries.Search
{
    // أمر بدء دراسة مشروع (PIO) — واحد لكل رد معتمد (التوصية ليست «غير متاح»)، يرتبط بالرد فقط.
    // يوقّعه رئيس القسم المختص ثم رئيس الفرع. المهندسون مستخدمون محددون (فريق الدراسة).
    public record SearchStudyStartOrdersQuery(StudyDocumentFilterDto Filter) : IRequest<PagedResultDto<StudyStartOrderDto>>;
}
