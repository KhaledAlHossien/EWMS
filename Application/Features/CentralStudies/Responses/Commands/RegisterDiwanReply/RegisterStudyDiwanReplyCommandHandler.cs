using Application.DTOs.Response;
using Application.Features.MapLayers;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Responses.Commands.RegisterDiwanReply
{
    public sealed class RegisterStudyDiwanReplyCommandHandler(IFileStore files, StudyDiwanSupport diwanSupport) : IRequestHandler<RegisterStudyDiwanReplyCommand, StudyResponseDto>
    {
        public async Task<StudyResponseDto> Handle(RegisterStudyDiwanReplyCommand request, CancellationToken ct)
        {
            var (response, viewer, version) = await diwanSupport.LoadForActionAsync(request.Id, StudyPermissions.ManageDiwan, request.RowVersion);
            var diwan = response.DiwanCase;
            if (diwan?.Stage is not (StudyDiwanStage.AwaitingReply or StudyDiwanStage.CorrectionRequested))
                throw new InvalidOperationException("تُسجّل نتيجة الرد المركزي بعد تسجيل الصادر أو عند إعادة المعاملة للتصحيح");
            var number = StudyDiwanSupport.ValidateReference(request.Number, request.Date);
            if (!Enum.IsDefined(request.Decision)) throw new InvalidOperationException("اختر موافقة أو رفض وفق الكتاب المركزي");

            string? originalName = null;
            await using var staged = await files.StageAsync(FileAreas.Studies, $"documents/{response.Assignment.RequestId}",
                async (destination, token) => originalName = await request.Upload.CopyFileToAsync(destination, token), ct);
            if (originalName == null || staged.Size == 0)
                throw new InvalidOperationException("أرفق صورة كتاب الرد المركزي");
            var fileName = MapFileRules.SafeFileName(originalName);
            MapFileRules.Detected? detected;
            await using (var content = staged.OpenRead()) detected = MapFileRules.Detect(content, fileName);
            if (detected == null || !detected.Kinds.Any(kind => kind is "images" or "pdf"))
                throw new InvalidOperationException("أرفق صورة كتاب الرد المركزي بصيغة PDF أو صورة");
            await staged.CommitAsync();

            diwan.Replies.Add(new StudyDiwanReply
            {
                ResponseId = response.Id,
                Number = number,
                Date = request.Date,
                Decision = request.Decision,
                FileName = fileName,
                ContentType = detected.ContentType,
                StorageKey = staged.Key,
                RecordedAt = DateTime.Now
            });
            diwan.Stage = StudyDiwanStage.AwaitingBranchHead;
            diwan.ReviewNote = null;
            var decision = request.Decision == StudyDiwanDecision.Approved ? "موافقة" : "رفض";
            return await diwanSupport.SaveStepAsync(response, viewer, version, StudyLogAction.Submitted, $"سُجّل الرد المركزي بالرقم {number} وأُحيل إلى رئيس الفرع",
                $"{decision} بموجب كتاب مؤرّخ {request.Date:yyyy-MM-dd}", notifyNext: true, file: staged);
        }
    }
}
