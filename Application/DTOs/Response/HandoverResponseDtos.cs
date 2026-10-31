namespace Application.DTOs.Response
{
    /// <summary>سطر في قائمة محاضر الاستلام</summary>
    public class HandoverListItemDto
    {
        public int Id { get; set; }
        /// <summary>الرقم الداخلي HND-…</summary>
        public string Number { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public int? ContractId { get; set; }
        public string? ContractNumber { get; set; }
        public int Kind { get; set; }
        public string KindAr { get; set; } = string.Empty;
        public int Status { get; set; }
        public string StatusAr { get; set; } = string.Empty;
        public int? Result { get; set; }
        public string ResultAr { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public DateTime? InspectionDate { get; set; }
        public int TotalNotes { get; set; }
        /// <summary>ملاحظات لم تُغلق (مفتوحة أو عولجت بانتظار التحقق)</summary>
        public int OpenNotes { get; set; }
        /// <summary>فيه ملاحظة تجاوزت موعد معالجتها (مرشّح متابعة، لا قرار)</summary>
        public bool HasOverdueNote { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class HandoverNoteDto
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public int? ResponsibleUserId { get; set; }
        /// <summary>اسم الموظف المسؤول أو الجهة المسؤولة</summary>
        public string? ResponsibleName { get; set; }
        public DateTime? DueDate { get; set; }
        public int Status { get; set; }
        public string StatusAr { get; set; } = string.Empty;
        public bool IsOverdue { get; set; }
        public DateTime? FixedAt { get; set; }
        public string? FixedByName { get; set; }
        public string FixNote { get; set; } = string.Empty;
        public DateTime? VerifiedAt { get; set; }
        public string? VerifiedByName { get; set; }
        public int? EvidenceVersionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool CanEdit { get; set; }
        public bool CanRemove { get; set; }
        public bool CanFix { get; set; }
        public bool CanVerify { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    public class HandoverDetailDto : HandoverListItemDto
    {
        public string RequestedByName { get; set; } = string.Empty;
        public string RequestNote { get; set; } = string.Empty;
        public int? CorrespondenceId { get; set; }
        public string? CorrespondenceNumber { get; set; }
        public string ResultNote { get; set; } = string.Empty;
        public int? MinutesVersionId { get; set; }
        public int? SupervisionReportVersionId { get; set; }
        public string? ConcludedByName { get; set; }
        public DateTime? ConcludedAt { get; set; }
        public CommitteeDto? Committee { get; set; }
        public List<HandoverNoteDto> Notes { get; set; } = [];

        // أعلام الأزرار: بالقواعد نفسها التي يفرضها الخادم
        public bool CanFormCommittee { get; set; }
        public bool CanEditCommittee { get; set; }
        public bool CanConclude { get; set; }
        public bool CanCancel { get; set; }
        public bool CanAddNote { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>ما لم يكتمل قبل إغلاق المشروع (تنبيه لا منع)</summary>
    public class ClosureWarningsDto
    {
        public int ProjectId { get; set; }
        public List<string> Warnings { get; set; } = [];
    }
}
