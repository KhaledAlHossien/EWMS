using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class User
    {
        public int Id { get; set; }
        public int PersonalIdNumber { get; set; }
        public required string FullName { get; set; }
        public required string Email { get; set; }
        public required string PasswordHash { get; set; }

        public int PhoneNumber { get; set; }

        public required int RoleId { get; set; }
        public Role Role { get; set; } = null!;

        // التبعية التنظيمية تأتي من الدور المرتبط بوحدة تنظيمية محددة.
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

