using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Permission
    {
        public int Id { get; set; }
        public required string Name { get; set; }

        public string Description { get; set; } = string.Empty;
    }
}
