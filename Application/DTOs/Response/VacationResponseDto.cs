using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Response
{
    public class VacationResponseDto
    {
        public int Id { get; set; }
        public string VacationTypeName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;

        // ===== سير العمل =====
        public string Status { get; set; } = string.Empty;
        public string StatusAr { get; set; } = string.Empty;
        public string CurrentStage { get; set; } = string.Empty;

        // ===== الدفع =====
        public bool IsPaid { get; set; }
        public string PaymentStatusAr => IsPaid ? "مدفوعة" : "غير مدفوعة";
        public bool ManagerAccept { get; set; }
        
        public bool BranchManagerAccept { get; set; }

        public string? RejectionReason { get; set; }
        public string? RejectedByName { get; set; }

        // ===== بيانات الإجازة =====
        public string VacReason { get; set; } = string.Empty;
        public int VacDayCount { get; set; }
        public DateTime StartVac { get; set; }
        public DateTime EndVac { get; set; }
        public DateTime CreatedAt { get; set; }

       
        
    }
}
