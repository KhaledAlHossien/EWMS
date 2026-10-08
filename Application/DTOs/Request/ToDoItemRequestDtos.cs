namespace Application.DTOs.Request
{
    public class AddToDoItemRequestDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Note { get; set; }
        public bool IsImportant { get; set; }
        public DateTime? DueDate { get; set; }
        /// <summary>1 يومي، 2 أسبوعي، 3 شهري — يحتاج DueDate (null/0 = بلا تكرار)</summary>
        public int? Repeat { get; set; }
    }

    /// <summary>تعديل بند: ما يُرسل فقط يتغيّر. لحذف الموعد أرسل ClearDueDate (يلغي التكرار معه)، ولإلغاء التكرار أرسل Repeat = 0</summary>
    public class UpdateToDoItemRequestDto
    {
        public string? Title { get; set; }
        public bool? IsDone { get; set; }
        public string? Note { get; set; }
        public bool? IsImportant { get; set; }
        public DateTime? DueDate { get; set; }
        public bool ClearDueDate { get; set; }
        public int? Repeat { get; set; }
    }

    /// <summary>الترتيب الجديد: كل أرقام بنود القائمة بلا زيادة ولا نقص</summary>
    public class ReorderToDoItemsRequestDto
    {
        public List<int> ItemIds { get; set; } = [];
    }

    /// <summary>إضافة عدة بنود دفعة واحدة (كل عنصر بند)</summary>
    public class BulkAddToDoItemsRequestDto
    {
        public List<string> Titles { get; set; } = [];
    }

    /// <summary>إضافة مهمة من لوحة المهام إلى القائمة كبند مرتبط بها</summary>
    public class AddTaskToToDoListRequestDto
    {
        public int TaskId { get; set; }
    }

    public class SetToDoFlagRequestDto
    {
        public bool Value { get; set; }
    }
}
