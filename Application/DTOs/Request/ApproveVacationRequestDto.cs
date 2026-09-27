using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Request
{
    public class ApproveVacationRequestDto
    {
        public bool Approve { get; set; }
        public string? Reason { get; set; }  // سبب الرفض
    }
}
