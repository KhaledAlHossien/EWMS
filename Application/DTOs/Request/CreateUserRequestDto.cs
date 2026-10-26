using Domain.Enums;

namespace Application.DTOs.Request
{
    public class CreateUserRequestDto
    {
        /// <summary>حساب نظام (1) أو موظف بلا دخول (2) — عامل المياومة يُضاف من صفحة الأشخاص في الحضور</summary>
        public UserKind Kind { get; set; } = UserKind.Account;
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }               // لحساب النظام وحده
        public string? PersonalIdNumber { get; set; }   // الرقم الذاتي (فريد، إلزامي للموظف الرسمي الجديد)
        public string? PhoneNumber { get; set; }        // رقم التواصل (هاتف سوري، اختياري)
        public string? Password { get; set; }           // لحساب النظام وحده
        public int? RoleId { get; set; }               // لحساب النظام وحده
        public int? DepartmentId { get; set; }
        public int? OfficeId { get; set; }
        public int? BranchId { get; set; }
    }
}
