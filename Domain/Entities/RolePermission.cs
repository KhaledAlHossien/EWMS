using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class RolePermission
    {
        public int Id { get; set; }
        
        public required int RoleId { get; set; }
        public Role Role { get; set; } = null!;


        public required int PermissionId { get; set; }
        public Permission Permission { get; set; } = null!;
    }
}
