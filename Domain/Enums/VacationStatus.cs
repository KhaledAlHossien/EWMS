using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Enums
{
    public enum VacationStatus
    {
        Unspecified = 0,
        PendingManager = 1,          // بانتظار رئيس القسم
        // القيمة 2 محجوزة سابقاً لمرحلة "الرئيس الإداري" المحذوفة من سير العمل — لا تُعاد استخدامها
        PendingBranchManager = 3,       // بانتظار رئيس الفرع
        Approved = 4,                // معتمدة نهائياً ✅
        Rejected = 5,                // مرفوضة ❌
        Cancelled = 6                // ألغاها الموظف بنفسه قبل اعتماد رئيس الفرع نهائياً
    }
}
