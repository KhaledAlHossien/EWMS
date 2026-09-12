using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Role
    {
        public int Id { get; set; }
        public required string Name { get; set; }
    }
}
