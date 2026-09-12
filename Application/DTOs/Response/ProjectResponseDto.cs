using Domain.Enums;

namespace Application.DTOs.Response
{
    public class ProjectResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ProjectStatus Status { get; set; }
        public int CreatedById { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public int CurrentDepartmentId { get; set; }
        public string CurrentDepartmentName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<ProjectAssignmentResponseDto> Assignments { get; set; } = [];
        public List<ProjectFileResponseDto> Files { get; set; } = [];
        public List<ProjectTransferResponseDto> Transfers { get; set; } = [];
    }
}
