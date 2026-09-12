namespace Application.DTOs.Response
{
    public class RoleResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<PermissionResponseDto> Permissions { get; set; } = [];
    }
}
