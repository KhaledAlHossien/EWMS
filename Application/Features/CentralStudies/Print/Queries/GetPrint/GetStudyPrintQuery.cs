using Application.DTOs.Response;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Print.Queries.GetPrint
{
    /// <summary>
    /// طباعة نموذج ورقي من مستندات الدراسات (الطلب، التكليف، الرد، أمر البدء، اعتماد المخططات) على نمط Vacations/Print:
    /// تحتاج PrintStudyDocuments مع صلاحية عرض المستند نفسه، وحدّها حدّ عرضه (فرع الدراسة).
    /// تُرجع بيانات المستند كاملة وترويسة الطباعة، وكل توقيع بصورة النسخة المحفوظة وقت القرار لا التوقيع الحالي.
    /// </summary>
    public record GetStudyPrintQuery(StudyEntityType Type, int Id) : IRequest<StudyPrintDto>;
}
