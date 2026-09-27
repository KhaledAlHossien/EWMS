using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Response
{
    public class VacationTypeResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsPaid { get; set; }   // ⬅️ جديد

        public string PaymentTypeAr => IsPaid ? "مدفوعة" : "غير مدفوعة";
    }
}
