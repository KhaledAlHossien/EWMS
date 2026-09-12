namespace Application.DTOs.Request
{
    public class UpdateRoleRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public List<int> PermissionIds { get; set; } = [];
    }
}
