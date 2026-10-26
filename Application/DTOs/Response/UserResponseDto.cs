namespace Application.DTOs.Response
{
    public class UserResponseDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        /// <summary>1 حساب نظام، 2 موظف بلا دخول، 3 عامل مياومة (Domain.Enums.UserKind)</summary>
        public int Kind { get; set; }
        public string? Email { get; set; }
        public string? PersonalIdNumber { get; set; }
        public string? PhoneNumber { get; set; }
        public int? RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public int? DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int? OfficeId { get; set; }
        public string OfficeName { get; set; } = string.Empty;
        public int? BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        /// <summary>الدخول موقوف مؤقتاً حتى (UTC) بعد محاولات خاطئة متكررة — فارغ إن لم يكن مقفلاً الآن</summary>
        public DateTime? LockedUntil { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
