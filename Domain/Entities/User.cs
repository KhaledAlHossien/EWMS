using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class User
    {
        public int Id { get; set; }
        public required string FullName { get; set; }
        public required string Email { get; set; }
        public required string PasswordHash { get; set; }

        public required int RoleId { get; set; }
        public Role Role { get; set; } = null!;

        public required int DepartmentId { get; set; }
        public Department Department { get; set; } = null!;

        public required int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public bool IsActive { get; set; }=true;

        public DateTime CreatedAt { get; set; }


    }
}

