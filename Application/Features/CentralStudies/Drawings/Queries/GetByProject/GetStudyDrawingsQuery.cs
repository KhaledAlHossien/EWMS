using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Drawings.Queries.GetByProject
{
    // المخططات: كل نوع صنف مستقل (TPH). القسم المُصدِر قسم مشارك في المشروع، ويعدّل مخططاته قسمه فقط.
    // المعماري يصدر أولاً بنسخ Draft-N يعتمد كلاً منها CDA (القسم المرسل ← إدارة التصميم ← ضبط الجودة)،
    // وغير المعماري «مبني على» نسخة معمارية معتمدة، وتراجعه صلاحية ReviewStudyDrawing.
    public record GetStudyDrawingsQuery(int ProjectId, int? Kind) : IRequest<List<StudyDrawingDto>>;
}
