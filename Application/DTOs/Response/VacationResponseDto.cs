using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Response
{
    public class VacationResponseDto
    {
        public int Id { get; set; }
        public string RequestNumber { get; set; } = string.Empty;   // VAC-2026-00012
        public int UserId { get; set; }
        public int VacationTypeId { get; set; }
        public string VacationTypeName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;

        // ===== سير العمل =====
        public string Status { get; set; } = string.Empty;
        public string StatusAr { get; set; } = string.Empty;
        public string CurrentStage { get; set; } = string.Empty;

        // ===== الدفع (يُحدَّد عند الاعتماد النهائي) =====
        public bool PaymentDecided { get; set; }        // = معتمدة
        public bool IsPaid { get; set; }                // فيها يوم مدفوع واحد على الأقل (بعد الاعتماد)
        public int PaidDays { get; set; }
        public int UnpaidDays { get; set; }
        public string PaymentStatusAr { get; set; } = string.Empty;
        public List<VacationSegmentDto> Segments { get; set; } = new();
        public bool ManagerAccept { get; set; }
        
        public bool BranchManagerAccept { get; set; }

        public string? FirstApprovedByName { get; set; }
        public DateTime? FirstApprovedAt { get; set; }
        public string? FinalApprovedByName { get; set; }
        public DateTime? FinalApprovedAt { get; set; }

        public string? RejectionReason { get; set; }
        public string? RejectedByName { get; set; }
        public DateTime? RejectedAt { get; set; }

        // ===== بيانات الإجازة =====
        public string VacReason { get; set; } = string.Empty;
        public int VacDayCount { get; set; }   // أيام العمل (بلا جمعة ولا عطل رسمية)
        public int CalendarDays { get; set; }
        public DateTime StartVac { get; set; }
        public DateTime EndVac { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class VacationSegmentDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsPaid { get; set; }
        public int Days { get; set; }
    }
}
