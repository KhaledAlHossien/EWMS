using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Office
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public required int DepartmentId { get; set; }
        public Department Department { get; set; } = null!;
    }
}
