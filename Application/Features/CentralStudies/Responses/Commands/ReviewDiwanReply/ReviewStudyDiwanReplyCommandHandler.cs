using Application.DTOs.Response;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Responses.Commands.ReviewDiwanReply
{
    public sealed class ReviewStudyDiwanReplyCommandHandler(StudyDiwanSupport diwanSupport) : IRequestHandler<ReviewStudyDiwanReplyCommand, StudyResponseDto>
    {
        public async Task<StudyResponseDto> Handle(ReviewStudyDiwanReplyCommand request, CancellationToken ct)
        {
            var (response, viewer, version) = await diwanSupport.LoadForActionAsync(request.Id, StudyDiwanRules.HeadPermission, request.RowVersion);
            var diwan = response.DiwanCase;
            var reply = StudyDiwanRules.LatestReply(response);
            if (diwan?.Stage != StudyDiwanStage.AwaitingBranchHead || reply == null)
                throw new InvalidOperationException("لا يوجد رد مركزي بانتظار إجراء رئيس الفرع");
            if (!Enum.IsDefined(request.Action)) throw new InvalidOperationException("اختر إجراء رئيس الفرع");
            var note = StudyRules.Clean(request.Note);
            if (note?.Length > 1000) throw new InvalidOperationException("ملاحظة رئيس الفرع لا تتجاوز 1000 حرف");

            if (request.Action == StudyDiwanReviewAction.ReturnForCorrection)
            {
                if (note == null) throw new InvalidOperationException("اذكر سبب الإعادة إلى الديوان");
                diwan.Stage = StudyDiwanStage.CorrectionRequested;
            }
            else
            {
                diwan.Stage = reply.Decision == StudyDiwanDecision.Rejected ? StudyDiwanStage.Rejected
                    : response.Recommendation == StudyRecommendation.NotAvailable ? StudyDiwanStage.ClosedWithoutStart
                    : StudyDiwanStage.Referred;
            }
            diwan.ReviewNote = note;
            if (diwan.Stage == StudyDiwanStage.Rejected)
            {
                // الرفض المركزي ليس نهائياً: يعود الرد إلى القسم المتابع ليعدّله، ثم يمر بالتواقيع ويُرسل بصادر جديد
                response.Status = StudyDocumentStatus.Rejected;
            }
            var action = diwan.Stage switch
            {
                StudyDiwanStage.CorrectionRequested => StudyLogAction.Edited,
                StudyDiwanStage.Referred => StudyLogAction.Approved,
                StudyDiwanStage.Rejected => StudyLogAction.Rejected,
                _ => StudyLogAction.Closed
            };
            return await diwanSupport.SaveStepAsync(response, viewer, version, action,
                $"{StudyDiwanRules.Name(diwan.Stage)} — الكتاب المركزي {reply.Number}", note, notifyNext: true);
        }
    }
}
