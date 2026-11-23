using Application.Common;
using Application.DTOs.Response;
using Application.Features.MapLayers;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.CentralStudiesBranch;
using Domain.Enums;
using MediatR;

namespace Application.Features.CentralStudies.Responses;

public record RegisterStudyDiwanDispatch(int Id, string Number, DateOnly Date, string RowVersion) : IRequest<StudyResponseDto>;
public record RegisterStudyDiwanReply(int Id, string Number, DateOnly Date, StudyDiwanDecision Decision, string RowVersion, IUploadReader Upload)
    : IRequest<StudyResponseDto>;
public enum StudyDiwanReviewAction { Confirm = 1, ReturnForCorrection = 2 }
public record ReviewStudyDiwanReply(int Id, StudyDiwanReviewAction Action, string? Note, string RowVersion) : IRequest<StudyResponseDto>;
public record GetStudyDiwanReplyFile(int Id, int ReplyId) : IRequest<StudyAttachmentContentDto>;

public sealed class StudyDiwanCommands(
    IStudyResponseService responses,
    StudyResponseSupport support,
    StudyKit kit,
    INotificationPusher pusher,
    IFileStore files)
    : IRequestHandler<RegisterStudyDiwanDispatch, StudyResponseDto>, IRequestHandler<RegisterStudyDiwanReply, StudyResponseDto>,
      IRequestHandler<ReviewStudyDiwanReply, StudyResponseDto>, IRequestHandler<GetStudyDiwanReplyFile, StudyAttachmentContentDto>
{
    public async Task<StudyResponseDto> Handle(RegisterStudyDiwanDispatch request, CancellationToken ct)
    {
        var (response, viewer, version) = await LoadForActionAsync(request.Id, StudyPermissions.ManageDiwan, request.RowVersion);
        if (StudyDiwanRules.StageOf(response) != StudyDiwanStage.AwaitingDispatch)
            throw new InvalidOperationException("سُجّل الصادر لهذه المعاملة مسبقاً");

        var number = ValidateReference(request.Number, request.Date);
        response.DiwanCase = new StudyDiwanCase
        {
            ResponseId = response.Id,
            Stage = StudyDiwanStage.AwaitingReply,
            DispatchNumber = number,
            DispatchDate = request.Date
        };
        return await SaveStepAsync(response, viewer, version, StudyLogAction.Submitted, $"سُجّل الصادر إلى الديوان المركزي بالرقم {number}",
            $"تاريخ الصادر {request.Date:yyyy-MM-dd}");
    }

    public async Task<StudyResponseDto> Handle(RegisterStudyDiwanReply request, CancellationToken ct)
    {
        var (response, viewer, version) = await LoadForActionAsync(request.Id, StudyPermissions.ManageDiwan, request.RowVersion);
        var diwan = response.DiwanCase;
        if (diwan?.Stage is not (StudyDiwanStage.AwaitingReply or StudyDiwanStage.CorrectionRequested))
            throw new InvalidOperationException("تُسجّل نتيجة الرد المركزي بعد تسجيل الصادر أو عند إعادة المعاملة للتصحيح");
        var number = ValidateReference(request.Number, request.Date);
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
        return await SaveStepAsync(response, viewer, version, StudyLogAction.Submitted, $"سُجّل الرد المركزي بالرقم {number} وأُحيل إلى رئيس الفرع",
            $"{decision} بموجب كتاب مؤرّخ {request.Date:yyyy-MM-dd}", notifyNext: true);
    }

    public async Task<StudyResponseDto> Handle(ReviewStudyDiwanReply request, CancellationToken ct)
    {
        var (response, viewer, version) = await LoadForActionAsync(request.Id, StudyDiwanRules.HeadPermission, request.RowVersion);
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
        var action = diwan.Stage switch
        {
            StudyDiwanStage.CorrectionRequested => StudyLogAction.Edited,
            StudyDiwanStage.Referred => StudyLogAction.Approved,
            _ => StudyLogAction.Closed
        };
        return await SaveStepAsync(response, viewer, version, action,
            $"{StudyDiwanRules.Name(diwan.Stage)} — الكتاب المركزي {reply.Number}", note, notifyNext: true);
    }

    public async Task<StudyAttachmentContentDto> Handle(GetStudyDiwanReplyFile request, CancellationToken ct)
    {
        var viewer = await kit.ViewerAsync();
        StudyRules.Require(viewer, StudyPermissions.ViewResponses);
        var response = await support.LoadAsync(request.Id, viewer);
        var reply = response.DiwanCase?.Replies.SingleOrDefault(r => r.Id == request.ReplyId)
            ?? throw new KeyNotFoundException("كتاب الرد المركزي غير موجود");
        return new(reply.FileName, reply.ContentType, files.OpenRead(reply.StorageKey), true);
    }

    private async Task<(StudyAssignmentResponse Response, Viewer Viewer, byte[] Version)> LoadForActionAsync(int id, string permission, string rowVersion)
    {
        var viewer = await kit.ViewerAsync();
        var response = await support.LoadAsync(id, viewer);
        if (!StudyDiwanRules.CanAct(viewer, permission, response.Assignment.Request.BranchId))
            throw new UnauthorizedAccessException("لا تملك صلاحية تنفيذ هذا الإجراء في فرع الدراسة");
        StudyRules.EnsureApproved(response.Status, "إشعار الرد");
        if (response.StartOrder != null) throw new InvalidOperationException("صدر أمر بدء لهذه الدراسة مسبقاً");

        var version = new byte[8];
        if (string.IsNullOrWhiteSpace(rowVersion) || !Convert.TryFromBase64String(rowVersion, version, out var count) || count != version.Length)
            throw new InvalidOperationException("أعد تحميل إشعار الرد قبل تسجيل الإجراء");
        if (!version.SequenceEqual(response.RowVersion))
            throw new InvalidOperationException("تغيّرت المعاملة بعد فتحها — أعد تحميل الصفحة ثم أعد المحاولة");
        return (response, viewer, version);
    }

    private static string ValidateReference(string value, DateOnly date)
    {
        var number = StudyRules.Clean(value);
        if (number == null || number.Length > 50 || date == default)
            throw new InvalidOperationException("رقم الكتاب (حتى 50 حرفاً) وتاريخه مطلوبان");
        return number;
    }

    private async Task<StudyResponseDto> SaveStepAsync(StudyAssignmentResponse response, Viewer viewer, byte[] version,
        StudyLogAction action, string title, string? details, bool notifyNext = false)
    {
        var study = response.Assignment.Request;
        var now = DateTime.Now;
        var activity = new StudyActivityLog
        {
            RequestId = study.Id,
            UserId = viewer.Id,
            EntityType = StudyEntityType.Response,
            EntityId = response.Id,
            Action = action,
            Title = title,
            Details = details,
            CreatedAt = now
        };
        var notifications = new List<Notification>();
        if (notifyNext && StudyDiwanRules.NextAction(response.DiwanCase?.Stage) is { } next)
        {
            var holders = await kit.DiwanRecipientsAsync(next.Permission, study.BranchId);
            notifications.AddRange(holders.Where(user => user.Id != viewer.Id).Select(user => new Notification
            {
                UserId = user.Id,
                Title = next.Title,
                Message = $"{response.ResponseNumber} — {study.MainBeneficiary}",
                Type = NotificationType.StudyNextStepReady,
                RelatedEntityType = "StudyResponse",
                RelatedEntityId = response.Id,
                CreatedAt = now
            }));
        }

        // تحديث نسخة إشعار الرد يسلسل انتقالات المعاملة مع الإلغاء وأي تعديل متزامن.
        response.UpdatedAt = now;
        await responses.SaveDiwanStepAsync(response, version, activity, notifications);
        if (notifications.Count > 0) await pusher.PushAsync(notifications);
        return await support.DetailAsync(response.Id, viewer);
    }
}
