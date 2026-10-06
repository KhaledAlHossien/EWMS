namespace Domain.Entities
{
    // قائمة مهام شخصية: صاحبها من أنشأها ولا يتغير
    public class ToDoList
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public required int UserId { get; set; }
        public User User { get; set; } = null!;

        public string Description { get; set; } = string.Empty;
    }
}
