namespace Application.DTOs.Response
{
    public class ToDoListResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string OwnerName { get; set; } = string.Empty;

        public int ItemsTotal { get; set; }
        public int ItemsDone { get; set; }
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
    }
}
