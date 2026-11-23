using Application.Common;
using Domain.Entities.CentralStudiesBranch;

namespace Application.Features.CentralStudies;

public static class StudyDiwanRules
{
    public const string HeadPermission = "SignStudyAsBranchHead";

    public static StudyDiwanStage? StageOf(StudyAssignmentResponse response) =>
        response.DiwanCase?.Stage ?? (response.Status == StudyDocumentStatus.Approved && response.StartOrder == null
            ? StudyDiwanStage.AwaitingDispatch : null);

    public static bool CanAct(Viewer viewer, string permission, int branchId) =>
        OrganizationRole.InBranch(viewer.User, branchId) && viewer.Has(permission) && viewer.Has(StudyPermissions.ViewResponses);

    public static bool ReadyForStartOrder(StudyAssignmentResponse response) =>
        response.Status == StudyDocumentStatus.Approved && response.Recommendation != StudyRecommendation.NotAvailable
        && response.DiwanCase?.Stage == StudyDiwanStage.Referred && LatestReply(response)?.Decision == StudyDiwanDecision.Approved;

    public static StudyDiwanReply? LatestReply(StudyAssignmentResponse response) =>
        response.DiwanCase?.Replies.MaxBy(reply => reply.Id);

    public static (string Permission, string Title)? NextAction(StudyDiwanStage? stage) => stage switch
    {
        StudyDiwanStage.AwaitingDispatch => (StudyPermissions.ManageDiwan, "دراسة بانتظار تسجيل الصادر إلى الديوان المركزي"),
        StudyDiwanStage.AwaitingReply => (StudyPermissions.ManageDiwan, "دراسة بانتظار تسجيل الرد المركزي"),
        StudyDiwanStage.AwaitingBranchHead => (HeadPermission, "رد مركزي بانتظار إجراء رئيس الفرع"),
        StudyDiwanStage.CorrectionRequested => (StudyPermissions.ManageDiwan, "أُعيدت معاملة الديوان للتصحيح"),
        StudyDiwanStage.Referred => ("CreateStudyStartOrder", "دراسة بانتظار إصدار أمر البدء"),
        _ => null
    };

    public static string Name(StudyDiwanStage stage) => stage switch
    {
        StudyDiwanStage.AwaitingDispatch => "بانتظار تسجيل الصادر",
        StudyDiwanStage.AwaitingReply => "بانتظار الرد المركزي",
        StudyDiwanStage.AwaitingBranchHead => "بانتظار إجراء رئيس الفرع",
        StudyDiwanStage.CorrectionRequested => "أُعيدت إلى ديوان الدراسات للتصحيح",
        StudyDiwanStage.Referred => "أُحيلت إلى إدارة المشاريع",
        StudyDiwanStage.Rejected => "أُغلقت بسبب الرفض المركزي",
        StudyDiwanStage.ClosedWithoutStart => "أُغلقت بتوصية فنية غير متاح",
        _ => throw new ArgumentOutOfRangeException(nameof(stage))
    };
}
