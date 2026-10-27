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
        VacationCancelled = 7,                // ألغى الموظف طلبه بنفسه
        WorkTaskAssigned = 8,                 // أُسندت للموظف مهمة عمل
        TaskAssigned = 9,                     // مهمة جديدة على لوحة المهام (للجهة المُسندة إليها)
        TaskStatusChanged = 10,               // تغيّرت حالة مهمة أسندتُها
        TaskCommented = 11,                   // تعليق جديد على مهمة
        MaintenanceRequestCreated = 12,       // طلب صيانة جديد (لرئيس القسم)
        MaintenanceAssigned = 13,             // نُقل طلب/مهمة صيانة إلى موظف آخر
        MaintenanceStatusChanged = 14,        // تغيّرت حالة طلب صيانة أو عُدّل
        MaintenanceTransferRequested = 15,    // طلب فني تحويل طلب صيانة (لرئيس القسم)
        MaintenanceTransferDecided = 16,      // قرار رئيس القسم على طلب التحويل (للفني)
        SparePartLowStock = 17,               // نزلت قطعة غيار تحت حدها الأدنى (لمن يُدخل المخزون في القسم)
        DeviceRepairCostThreshold = 18,       // (متوقف 2026-10-08: حُذف حد التكلفة من أنواع الأجهزة) يبقى للإشعارات القديمة فقط
        TaskDueSoon = 19,                     // مهمة يحين موعدها اليوم أو غداً
        TaskOverdue = 20,                     // تجاوزت مهمة موعد تسليمها
        TaskAttachmentAdded = 21,             // أُرفق ملف بمهمة
        TaskRecurrenceStopped = 22,           // توقفت مهمة دورية لتعذّر إنشائها (لصاحبها)
        ToDoItemDueSoon = 23,                 // بند في مفكرتي يستحق اليوم أو غداً
        ToDoItemOverdue = 24,                 // بند في مفكرتي تجاوز موعده
        LoginLocked = 25,                     // أُوقف الدخول إلى حسابك مؤقتاً بعد محاولات خاطئة متكررة (لصاحب الحساب)
        // 26–29 محجوزة لإشعارات الحضور (فرع attendance) — المشاريع تبدأ من 40 كي لا يتعارض الدمج
        ProjectMemberAdded = 40,              // أُضفت مشاركاً في مشروع
        ScheduleTaskOverdue = 41,             // بند في جدول مشروع تجاوز نهايته (مرشّح للتأخر: راجع حالته وسجّل التأخير)
        ScheduleJustificationSubmitted = 42,  // رُفع تبرير تأخير بانتظار الاعتماد
        ScheduleJustificationDecided = 43,    // قرار على تبرير تأخير سجّلتَه
        ScheduleExtensionSubmitted = 44,      // رُفع طلب تمديد بانتظار الاعتماد
        ScheduleExtensionDecided = 45         // قرار على طلب تمديد رفعتَه
    }
}
