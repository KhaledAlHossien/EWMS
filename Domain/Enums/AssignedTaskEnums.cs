namespace Domain.Enums
{
    /// <summary>حالة المهمة المُسندة (أعمدة لوحة المهام)</summary>
    public enum AssignedTaskStatus
    {
        Todo = 1,        // لم تُنفَّذ
        InProgress = 2,  // قيد التنفيذ
        Done = 3,        // تم التنفيذ (يعتمده المُسنِد)
        InReview = 4     // بانتظار مراجعة المُسنِد (أُضيفت بعد Done فبقيت الأرقام القديمة كما هي)
    }

    public enum AssignedTaskPriority
    {
        Low = 1,
        Normal = 2,
        High = 3,
        Urgent = 4
    }

    /// <summary>
    /// الجهة المُسندة إليها المهمة (نزولاً في الهيكل):
    /// رئيس الفرع ← قسم، رئيس القسم ← مكتب، رئيس المكتب ← موظف.
    /// مهمة القسم يتولاها رئيس القسم، ومهمة المكتب يتولاها رئيس المكتب.
    /// </summary>
    public enum AssignedTaskTargetType
    {
        Department = 1,
        Office = 2,
        User = 3
    }

    public enum AssignedTaskActivityType
    {
        Created = 1,
        StatusChanged = 2,
        Comment = 3,
        Delegated = 4,   // أُسندت منها مهمة فرعية لجهة أدنى
        Edited = 5,
        Attached = 6,          // أُرفق ملف
        AttachmentRemoved = 7, // حُذف مرفق
        Claimed = 8,           // «أتولّى هذه المهمة»
        Released = 9           // تخلّى عن توليها
    }
}

namespace Domain.Enums
{
    public enum TaskRecurrenceFrequency
    {
        Daily = 1,
        Weekly = 2,
        Monthly = 3
    }

    /// <summary>السجلات التي يمكن ربط مهمة بها</summary>
    public enum TaskLinkType
    {
        MaintenanceRequest = 1,
        Vacation = 2,
        Site = 3
    }
}
