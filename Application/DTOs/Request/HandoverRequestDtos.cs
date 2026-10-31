namespace Application.DTOs.Request
{
    /// <summary>طلب استلام (يقدّمه مهندس الموقع، ويمرّ عبر الديوان)</summary>
    public class HandoverRequestDto
    {
        public int ProjectId { get; set; }
        /// <summary>HandoverKind: 1 استلام أولي، 2 استلام نهائي</summary>
        public int Kind { get; set; } = 1;
        public int? ContractId { get; set; }
        /// <summary>معاملة الديوان التي مرّ بها الطلب (واردة، اختياري)</summary>
        public int? CorrespondenceId { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>تسجيل النتيجة: 1 بلا ملاحظات، 2 بملاحظات (تشترط ملاحظة مسجَّلة). يُثبَّت محضر اللجنة وتقرير الإشراف إصدارين من الإضبارة (اختياري)</summary>
    public class ConcludeHandoverRequestDto
    {
        public int Result { get; set; }
        public DateTime InspectionDate { get; set; }
        public string? ResultNote { get; set; }
        public int? MinutesVersionId { get; set; }
        public int? SupervisionReportVersionId { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class CancelHandoverRequestDto
    {
        public string? Reason { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>ملاحظة لجنة الاستلام: المسؤول موظف أو جهة بالاسم (واحد منهما، أو لا أحد بعد)</summary>
    public class HandoverNoteRequestDto
    {
        public string Description { get; set; } = string.Empty;
        public int? ResponsibleUserId { get; set; }
        public string? ResponsibleName { get; set; }
        public DateTime? DueDate { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>تسجيل معالجة ملاحظة: ما عولج، ووثيقة المعالجة (إصدار من الإضبارة، اختياري)</summary>
    public class FixHandoverNoteRequestDto
    {
        public string FixNote { get; set; } = string.Empty;
        public int? EvidenceVersionId { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>التحقق من المعالجة (الملاحظة اختيارية) أو رفضها (السبب مطلوب)</summary>
    public class VerifyHandoverNoteRequestDto
    {
        public string? Note { get; set; }
        public int? EvidenceVersionId { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
