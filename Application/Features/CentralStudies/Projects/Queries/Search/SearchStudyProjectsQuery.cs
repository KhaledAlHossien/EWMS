using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Queries.Search
{
    // المشروع — واحد لكل أمر بدء معتمد. يُنشأ بمراحله من أنواع المراحل الفعّالة (المرتبطة بمستند تُنشأ مغلقة من تواريخه)،
    // ويُضاف القسم المختص قسماً مشاركاً. الإنجاز يعتمده إدارة المشاريع ثم رئيس الفرع. حدّ الصلاحيات: مشاريع فرعه.
    public record SearchStudyProjectsQuery(StudyDocumentFilterDto Filter) : IRequest<PagedResultDto<StudyProjectDto>>;
}
