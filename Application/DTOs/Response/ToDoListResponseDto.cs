namespace Application.DTOs.Response
{
    public class ToDoListResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string OwnerName { get; set; } = string.Empty;
    }
}
