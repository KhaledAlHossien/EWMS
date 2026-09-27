using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Enums
{
    public enum VacationStatus
    {
        PendingManager = 1,          // بانتظار رئيس القسم
        PendingAdministrative = 2,   // بانتظار الرئيس الإداري
        PendingBranchManager = 3,       // بانتظار رئيس الفرع
        Approved = 4,                // معتمدة نهائياً ✅
        Rejected = 5                 // مرفوضة ❌
    }
}
