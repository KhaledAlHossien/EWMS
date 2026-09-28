namespace Application.DTOs.Response
{
    /// <summary>للإدارة (السوبر ادمن): المهمة مع الموظفين المسنَدة إليهم</summary>
    public class WorkTaskResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<WorkTaskAssigneeDto> Assignees { get; set; } = [];
    }

    public class WorkTaskAssigneeDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string OfficeName { get; set; } = string.Empty;
    }

    /// <summary>بطاقة مهمة في الداشبورد — بدون قائمة الموظفين</summary>
    public class WorkTaskCardDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public int AssigneesCount { get; set; }
    }
}
