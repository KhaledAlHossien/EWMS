using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.CoordinationHeads.Queries.GetCoordinationHeads
{
    /// <summary>
    /// رؤساء قسم التنسيق والارتباط في فرع الدراسة (من يملك التوقيع بصفته): قائمة اختيار مدير مشروع الفرع الجغرافي.
    /// الحد فرع المستخدم؛ من يتصفح المؤسسة يمرّر branchId.
    /// </summary>
    public record GetCoordinationHeadsQuery(int? BranchId) : IRequest<List<StudyPersonDto>>;
}
