using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Approvals.Queries.GetPending
{
    // التواقيع على مستندات الدراسات بالترتيب: لكل صفة صلاحية بحدّها (الفرع، أو قسم المستند للمتابع/المختص/المرسل)،
    // ولا يوقّع الشخص مرتين على المستند نفسه. الرفض من أي موقّع يرفض المستند، واكتمال الموافقات يعتمده.
    // التوقيع يحفظ نسخة توقيع الموقّع ومسماه لحظة القرار.
    public record GetPendingStudyApprovalsQuery : IRequest<List<StudyApprovalQueueItemDto>>;
}
