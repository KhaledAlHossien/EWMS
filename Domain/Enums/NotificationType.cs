namespace Domain.Enums
{
    public enum NotificationType
    {
        VacationSubmitted = 1,               // طلب إجازة جديد بانتظار رئيس القسم
        VacationApprovedByManager = 2,        // وافق رئيس القسم (ما زالت بانتظار رئيس الفرع)
        VacationForwardedToBranchManager = 3, // وصل الطلب لرئيس الفرع بعد موافقة رئيس القسم
        VacationRejectedByManager = 4,        // رفض رئيس القسم
        VacationApprovedFinal = 5,            // اعتماد نهائي من رئيس الفرع
        VacationRejectedByBranchManager = 6,  // رفض نهائي من رئيس الفرع
        VacationCancelled = 7                 // ألغى الموظف طلبه بنفسه
    }
}
