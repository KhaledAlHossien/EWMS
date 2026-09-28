namespace Application.DTOs.Request
{
    public class CreateWorkTaskRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = "📋";
        public int BranchId { get; set; }
        public bool IsActive { get; set; } = true;

        /// <summary>الموظفون المسنَدة إليهم المهمة — يجب أن يكونوا من نفس الفرع</summary>
        public List<int> UserIds { get; set; } = [];
    }
}
