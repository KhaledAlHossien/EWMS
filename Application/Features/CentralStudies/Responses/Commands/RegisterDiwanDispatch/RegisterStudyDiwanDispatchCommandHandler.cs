using Application.DTOs.Response;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Responses.Commands.RegisterDiwanDispatch
{
    public sealed class RegisterStudyDiwanDispatchCommandHandler(StudyDiwanSupport diwanSupport) : IRequestHandler<RegisterStudyDiwanDispatchCommand, StudyResponseDto>
    {
        public async Task<StudyResponseDto> Handle(RegisterStudyDiwanDispatchCommand request, CancellationToken ct)
        {
            var (response, viewer, version) = await diwanSupport.LoadForActionAsync(request.Id, StudyPermissions.ManageDiwan, request.RowVersion);
            if (StudyDiwanRules.StageOf(response) != StudyDiwanStage.AwaitingDispatch)
                throw new InvalidOperationException("سُجّل الصادر لهذه المعاملة مسبقاً");

            var number = StudyDiwanSupport.ValidateReference(request.Number, request.Date);
            // بعد رفض مركزي وتعديل الرد واعتماده من جديد: صادر جديد للمعاملة نفسها، وكتب الرد السابقة تبقى في سجلها
            var resend = response.DiwanCase != null;
            response.DiwanCase ??= new StudyDiwanCase { ResponseId = response.Id };
            response.DiwanCase.Stage = StudyDiwanStage.AwaitingReply;
            response.DiwanCase.DispatchNumber = number;
            response.DiwanCase.DispatchDate = request.Date;
            response.DiwanCase.ReviewNote = null;
            return await diwanSupport.SaveStepAsync(response, viewer, version, StudyLogAction.Submitted,
                resend ? $"أُعيد إرسال الرد المعدّل إلى الديوان المركزي بالصادر {number}" : $"سُجّل الصادر إلى الديوان المركزي بالرقم {number}",
                $"تاريخ الصادر {request.Date:yyyy-MM-dd}");
        }
    }
}
