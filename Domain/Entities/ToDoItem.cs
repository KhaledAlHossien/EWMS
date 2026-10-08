using Domain.Enums;

namespace Domain.Entities
{
    /// <summary>
    /// بند داخل قائمة مهام شخصية (قرار المستخدم 2026-10-08): عنوان وحالة «منجز» وترتيب،
    /// ثم (2026-10-08 «مفكرتي»): موعد وتذكير، ملاحظة وأولوية، تكرار، وربط اختياري بمهمة في لوحة المهام.
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

        public string Note { get; set; } = string.Empty;
        /// <summary>علامة «مهم»</summary>
        public bool IsImportant { get; set; }

        /// <summary>تاريخ الاستحقاق (يوم فقط)؛ منه يأتي «مهامي اليوم» والتذكيرات</summary>
        public DateTime? DueDate { get; set; }
        /// <summary>
        /// بند متكرر: عند تعليمه منجزاً لا يبقى منجزاً، بل يتقدّم موعده إلى الاستحقاق التالي ويعود مفتوحاً.
        /// يحتاج موعداً.
        /// </summary>
        public TaskRecurrenceFrequency? Repeat { get; set; }
        public DateTime? LastCompletedAt { get; set; }

        // تذكيرات الموعد: مرة واحدة لكل موعد، وتُصفَّر عند تغييره (كما في لوحة المهام)
        public DateTime? DueSoonNotifiedAt { get; set; }
        public DateTime? OverdueNotifiedAt { get; set; }

        /// <summary>مهمة من لوحة المهام أُضيفت إلى المفكرة (يُصفَّر إن حُذفت المهمة)</summary>
        public int? LinkedTaskId { get; set; }
        public AssignedTask? LinkedTask { get; set; }
    }
}
