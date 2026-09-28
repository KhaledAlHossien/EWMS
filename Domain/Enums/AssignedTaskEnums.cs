namespace Domain.Enums
{
    /// <summary>حالة المهمة المُسندة (أعمدة لوحة المهام)</summary>
    public enum AssignedTaskStatus
    {
        Todo = 1,        // لم تُنفَّذ
        InProgress = 2,  // قيد التنفيذ
        Done = 3         // تم التنفيذ
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
        Edited = 5
    }
}
