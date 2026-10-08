namespace Domain.Entities
{
    /// <summary>
    /// بند داخل قائمة مهام شخصية (قرار المستخدم 2026-10-08): عنوان وحالة «منجز» وترتيب.
    /// البند جزء من قائمته: يُحذف معها، وصاحبه صاحب القائمة.
    /// </summary>
    public class ToDoItem
    {
        public int Id { get; set; }

        public int ToDoListId { get; set; }
        public ToDoList ToDoList { get; set; } = null!;

        public string Title { get; set; } = string.Empty;
        public bool IsDone { get; set; }
        public DateTime? DoneAt { get; set; }

        /// <summary>الترتيب داخل القائمة (0 = الأول)، ويغيّره صاحب القائمة بالسحب</summary>
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
