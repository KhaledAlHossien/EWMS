namespace Application.DTOs.Response
{
    public class ToDoListResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public bool IsPinned { get; set; }
        public bool IsArchived { get; set; }

        public int ItemsTotal { get; set; }
        public int ItemsDone { get; set; }
        /// <summary>غير المنجزة التي تجاوزت موعدها</summary>
        public int ItemsOverdue { get; set; }
        /// <summary>أقرب موعد بين البنود غير المنجزة</summary>
        public DateTime? NextDueDate { get; set; }
        /// <summary>البنود مرتبة — تأتي في Get/{id} وعمليات البنود فقط، وفارغة في GetAll (الأعداد تكفي)</summary>
        public List<ToDoItemResponseDto> Items { get; set; } = [];
    }

    public class ToDoItemResponseDto
    {
        public int Id { get; set; }
        public int ToDoListId { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsDone { get; set; }
        public DateTime? DoneAt { get; set; }
        public int SortOrder { get; set; }

        public string Note { get; set; } = string.Empty;
        public bool IsImportant { get; set; }
        public DateTime? DueDate { get; set; }
        public bool IsOverdue { get; set; }
        /// <summary>Daily | Weekly | Monthly، أو null</summary>
        public string? Repeat { get; set; }
        public string? RepeatAr { get; set; }
        public DateTime? LastCompletedAt { get; set; }
        /// <summary>المهمة المرتبطة في لوحة المهام (إن وُجدت)؛ Available=false إن لم يعد يحق لك عرضها (بلا بياناتها)</summary>
        public ToDoLinkedTaskDto? LinkedTask { get; set; }
    }

    public class ToDoLinkedTaskDto
    {
        public int Id { get; set; }
        public bool Available { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusAr { get; set; } = string.Empty;
        public bool IsOverdue { get; set; }
    }

    /// <summary>«مهامي اليوم»: بنود قوائمي غير المنجزة المستحقة اليوم أو المتأخرة</summary>
    public class ToDoTodayItemDto : ToDoItemResponseDto
    {
        public string ListName { get; set; } = string.Empty;
        public string ListColor { get; set; } = string.Empty;
        public string ListIcon { get; set; } = string.Empty;
    }

    public class ToDoTodayDto
    {
        public int OverdueCount { get; set; }
        public int TodayCount { get; set; }
        public List<ToDoTodayItemDto> Items { get; set; } = [];
    }
}
