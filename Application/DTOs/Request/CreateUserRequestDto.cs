namespace Application.DTOs.Request
{
    public class CreateUserRequestDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PersonalIdNumber { get; set; }   // الرقم الذاتي (اختياري، فريد)
        public string? PhoneNumber { get; set; }        // رقم التواصل (هاتف سوري، اختياري)
        public string Password { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public int? DepartmentId { get; set; }
        public int? OfficeId { get; set; }
        public int? BranchId { get; set; }
    }
}
