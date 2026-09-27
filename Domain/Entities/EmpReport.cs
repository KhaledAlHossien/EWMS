using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class EmpReport
    {
        public int Id { get; set; }
        public required string FullName { get; set; }
        public required string Email { get; set; }

        public string Description { get; set; } = string.Empty;

        public int DepartmentId { get; set; }
        public Department Department { get; set; } = null!;

        public int TaskId { get; set; }
        public Tasks Task { get; set; } = null!;
    }
}
