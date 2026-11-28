using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.StudyBranches.Queries.GetAll
{
    /// <summary>أفرع الدراسات ضمن حدود المستخدم (فرعه، أو كل الفروع لنظرة المؤسسة مع تصفية اختيارية بالفرع)</summary>
    public record GetStudyBranchesQuery(int? BranchId, bool ActiveOnly) : IRequest<List<StudyBranchDto>>;
}
