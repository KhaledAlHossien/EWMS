using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Stages.Queries.GetByProject
{
    // مراحل المشروع (متتالية: لا تُغلق مرحلة قبل ما قبلها، وتغلقها إدارة المشاريع) ومهامها (حرّة داخل المرحلة).
    // مهام المراحل منفصلة عن لوحة مهام EWMS وبنفس آلية الإسناد: قسم مشارك، مكتب منه، أو عضو في الفريق.
    public record GetStudyStagesQuery(int ProjectId) : IRequest<List<ProjectStageDto>>;
}
