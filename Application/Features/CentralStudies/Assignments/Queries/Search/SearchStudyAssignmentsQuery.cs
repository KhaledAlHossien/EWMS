using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Assignments.Queries.Search
{
    // التكليف الداخلي لبيان مشروع (IPA) — واحد لكل طلب معتمد، يعتمده رئيس الفرع. حدّ الصلاحيات: تكليفات طلبات فرعه.
    public record SearchStudyAssignmentsQuery(StudyDocumentFilterDto Filter) : IRequest<PagedResultDto<StudyAssignmentDto>>;
}
