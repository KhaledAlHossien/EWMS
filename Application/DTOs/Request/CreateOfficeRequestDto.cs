namespace Application.DTOs.Request
{
    public class CreateOfficeRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int DepartmentId { get; set; }
    }
}
