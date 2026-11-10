using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Responses.Queries.Search
{
    // إشعار الرد على التكليف — رد واحد لكل تكليف معتمد ولا يُعاد. يوقّعه القسم المتابع ثم التخطيط وإدارة المشاريع.
    // أجزاؤه تُزامن مع النموذج في كل حفظ (المباني بنوعها، الكادر باختصاصه، والبنود تُستبدل).
    public record SearchStudyResponsesQuery(StudyDocumentFilterDto Filter) : IRequest<PagedResultDto<StudyResponseDto>>;
}
