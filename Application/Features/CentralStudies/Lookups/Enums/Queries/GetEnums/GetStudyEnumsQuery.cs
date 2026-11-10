using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.Enums.Queries.GetEnums
{
    /// <summary>قيم كل تعدادات فرع الدراسات وأسماؤها (لقوائم الواجهة)</summary>
    public record GetStudyEnumsQuery : IRequest<Dictionary<string, List<StudyEnumValueDto>>>;
}
