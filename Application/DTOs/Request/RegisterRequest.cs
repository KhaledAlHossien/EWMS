using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Request
{
    public class RegisterRequest
    {
        public required string FullName { get; set; }
        public required string Email { get; set; }
        public required string Password { get; set; }
        public required int RoleId { get; set; }
        public required int DepartmentId { get; set; }
        public required int BranchId { get; set; }
    }
}
