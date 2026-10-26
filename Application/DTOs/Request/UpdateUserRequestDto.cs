using Domain.Enums;

namespace Application.DTOs.Request
{
    public class UpdateUserRequestDto
    {
        /// <summary>حساب نظام (1) أو موظف بلا دخول (2) — التحويل إلى حساب يحتاج بريداً وكلمة مرور ودوراً</summary>
        public UserKind Kind { get; set; } = UserKind.Account;
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? PersonalIdNumber { get; set; }   // الرقم الذاتي (اختياري، فريد)
        public string? PhoneNumber { get; set; }        // رقم التواصل (هاتف سوري، اختياري)
        public string? Password { get; set; }
        public int? RoleId { get; set; }
        public int? DepartmentId { get; set; }
        public int? OfficeId { get; set; }
        public int? BranchId { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
