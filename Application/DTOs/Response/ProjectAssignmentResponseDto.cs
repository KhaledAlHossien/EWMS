using Domain.Enums;

namespace Application.DTOs.Response
{
    public class ProjectAssignmentResponseDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public int AssignedUserId { get; set; }
        public string AssignedUserName { get; set; } = string.Empty;
        public int AssignedByUserId { get; set; }
        public string AssignedByUserName { get; set; } = string.Empty;
        public AssignmentStatus Status { get; set; }
        public DateTime AssignedAt { get; set; }
    }
}
