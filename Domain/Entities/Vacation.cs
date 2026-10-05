using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Vacation
    {
        public int Id { get; set; }

        public required int VacationTypeId { get; set; }
        public VacationType VacationType { get; set; } = null!;

        public required int UserId { get; set; }
        public User User { get; set; } = null!;

        public required int DepartmentId { get; set; }
        public Department Department { get; set; } = null!;

        public required int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        // ===== سجل الموافقات (Audit Trail) =====
        public bool ManagerAccept { get; set; } = false;
        
        public bool BranchManagerAccept { get; set; } = false;  // ⚠️ غيّرنا الافتراضي إلى false

        // من وافق في المرحلة الأولى (صاحب ApproveVacationFirst) — null للإجازات القديمة أو التي تجاوزت المرحلة الأولى
        public int? FirstApprovedByUserId { get; set; }
        public User? FirstApprovedByUser { get; set; }
        public DateTime? FirstApprovedAt { get; set; }

        // من اعتمد الإجازة نهائياً ومتى (للطباعة والسجل)
        public int? FinalApprovedByUserId { get; set; }
        public User? FinalApprovedByUser { get; set; }
        public DateTime? FinalApprovedAt { get; set; }
        // توقيع المعتمِد كما كان لحظة الاعتماد (null = لم يكن له توقيع، فتُطبع الخانة فارغة)
        public int? FinalApprovedSignatureId { get; set; }
        public UserSignature? FinalApprovedSignature { get; set; }

        // ===== حالة سير العمل (جديد) =====
        public VacationStatus Status { get; set; } = VacationStatus.PendingManager;

        // ===== الدفع (يُحدَّد عند الاعتماد النهائي — قبله كلها أصفار/false) =====
        // IsPaid = في الإجازة يوم مدفوع واحد على الأقل. التفصيل في Segments.
        public bool IsPaid { get; set; } = false;
        public int PaidDays { get; set; }
        public int UnpaidDays { get; set; }
        public ICollection<VacationSegment> Segments { get; set; } = new List<VacationSegment>();

        // مرفقات الطلب (عند التقديم فقط)
        public ICollection<VacationAttachment> Attachments { get; set; } = new List<VacationAttachment>();

        // ===== معلومات الرفض =====
        public int? RejectedByUserId { get; set; }
        public User? RejectedByUser { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime? RejectedAt { get; set; }
        // المرحلة التي رُفض فيها الطلب (PendingManager = الأولى، PendingBranchManager = النهائية) — لنموذج الطباعة
        public VacationStatus? RejectedAtStage { get; set; }
        // توقيع من رفض كما كان لحظة الرفض
        public int? RejectedSignatureId { get; set; }
        public UserSignature? RejectedSignature { get; set; }

        // ===== بيانات الإجازة =====
        public string VacReason { get; set; } = string.Empty;
        public int VacDayCount { get; set; }          // أيام العمل في المدة (بلا جمعة ولا عطل رسمية)
        public DateTime StartVac { get; set; }
        public DateTime EndVac { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // يمنع قرارين متزامنين على نفس الطلب (يُرفض الثاني برسالة)
        public byte[] RowVersion { get; set; } = null!;
    }
}
