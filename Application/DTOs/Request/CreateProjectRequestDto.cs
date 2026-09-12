namespace Application.DTOs.Request
{
    public class CreateProjectRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int CurrentDepartmentId { get; set; }
        public List<int> AssignedUserIds { get; set; } = [];
    }
}
