using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class User
    {
        public int Id { get; set; }
        // الرقم الذاتي للموظف (قد يحوي حروفاً مثل M201160) — فريد، واختياري للحسابات القديمة
        public string? PersonalIdNumber { get; set; }
        public required string FullName { get; set; }
        public required string Email { get; set; }
        public required string PasswordHash { get; set; }

        // رقم التواصل (نص كي لا يضيع الصفر الأول): هاتف سوري، اختياري — PhoneRules
        public string? PhoneNumber { get; set; }

        public required int RoleId { get; set; }
        public Role Role { get; set; } = null!;

        // مكان الموظف في الهيكل (كلها اختيارية): المكتب يحدد القسم، والقسم يحدد الفرع — UserRules.EnsureUserReferencesAsync
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        public int? OfficeId { get; set; }
        public Office? Office { get; set; }

        public int? BranchId { get; set; }
        public Branch? Branch { get; set; }

        public bool IsActive { get; set; }=true;

        public DateTime CreatedAt { get; set; }


    }
}

