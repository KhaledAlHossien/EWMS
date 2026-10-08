namespace Domain.Entities
{
    // قائمة مهام شخصية («مفكرتي»): صاحبها من أنشأها ولا يتغير، وفيها بنود ToDoItem
    public class ToDoList
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public required int UserId { get; set; }
        public User User { get; set; } = null!;

        public string Description { get; set; } = string.Empty;

        /// <summary>بنود القائمة (عنوان + منجز + ترتيب …) — تُحذف معها</summary>
        public ICollection<ToDoItem> Items { get; set; } = new List<ToDoItem>();

        /// <summary>لون من لوحة ثابتة (green/blue/purple/orange/red/gray) أو فارغ</summary>
        public string Color { get; set; } = string.Empty;
        /// <summary>رمز تعبيري واحد اختياري</summary>
        public string Icon { get; set; } = string.Empty;
        public bool IsPinned { get; set; }
        /// <summary>مؤرشفة: تُخفى من القوائم، ولا تُعدَّل بنودها، ولا تذكيرات لها</summary>
        public bool IsArchived { get; set; }
    }
}
