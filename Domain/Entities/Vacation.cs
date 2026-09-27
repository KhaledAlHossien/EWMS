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

        // ===== حالة سير العمل (جديد) =====
        public VacationStatus Status { get; set; } = VacationStatus.PendingManager;

        // ===== الدفع =====
        public bool IsPaid { get; set; } = true;

        // ===== معلومات الرفض =====
        public int? RejectedByUserId { get; set; }
        public User? RejectedByUser { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime? RejectedAt { get; set; }

        // ===== بيانات الإجازة =====
        public string VacReason { get; set; } = string.Empty;
        public int VacDayCount { get; set; }
        public DateTime StartVac { get; set; }
        public DateTime EndVac { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
