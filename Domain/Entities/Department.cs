using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Department
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public required int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;
        public string Description { get; set; } = string.Empty;
    }
}
