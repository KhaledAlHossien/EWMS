namespace Domain.Entities.CentralStudiesBranch;

public enum StudyDiwanStage
{
    AwaitingDispatch = 1,
    AwaitingReply = 2,
    AwaitingBranchHead = 3,
    CorrectionRequested = 4,
    Referred = 5,
    Rejected = 6,
    ClosedWithoutStart = 7
}

public enum StudyDiwanDecision { Approved = 1, Rejected = 2 }

/// <summary>إجراء رئيس الفرع على الرد المركزي: إحالة (أو إثبات اطلاع) أو إعادة إلى الديوان للتصحيح</summary>
public enum StudyDiwanReviewAction { Confirm = 1, ReturnForCorrection = 2 }

public class StudyDiwanCase
{
    public int ResponseId { get; set; }
    public StudyAssignmentResponse Response { get; set; } = null!;
    public StudyDiwanStage Stage { get; set; }
    public string DispatchNumber { get; set; } = string.Empty;
    public DateOnly DispatchDate { get; set; }
    public string? ReviewNote { get; set; }
    public List<StudyDiwanReply> Replies { get; set; } = [];
}

public class StudyDiwanReply
{
    public int Id { get; set; }
    public int ResponseId { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public StudyDiwanDecision Decision { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
}
