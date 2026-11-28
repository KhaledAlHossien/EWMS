using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.DiwanTransactions.Commands.RequestRevision
{
    /// <summary>
    /// مدير المشاريع بعد الرفض المركزي: تعديل تخطيطي (يعود الرد إلى توقيع التخطيط وحده ويبقى توقيع القسم المتابع) أو
    /// تعديل فني (يعود الرد إلى القسم المتابع مع السبب ثم يوقّعه هو ثم التخطيط). بعد اعتماد الرد المعدّل تعود المعاملة إلى الديوان.
    /// </summary>
    public record RequestStudyDiwanRevisionCommand(int Id, StudyDiwanRevisionFormDto Dto) : IRequest<StudyDiwanTransactionDto>;
}
