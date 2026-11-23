namespace Application.DTOs.Response;

public record StudyDiwanReplyDto(int Id, string Number, DateOnly Date, int Decision, string FileName, DateTime RecordedAt);

public record StudyDiwanCaseDto(int Stage, string StageName, string? DispatchNumber, DateOnly? DispatchDate, string? ReviewNote,
    IReadOnlyList<StudyDiwanReplyDto> Replies, bool CanRegisterDispatch, bool CanRegisterReply, bool CanReview);
