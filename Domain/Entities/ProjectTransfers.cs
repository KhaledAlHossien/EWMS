using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class ProjectTransfers
    {
        public int Id { get; set; }

        public required int ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        public required int FromDepartmentId { get; set; }
        public Department FromDepartment { get; set; } = null!;


        public required int ToDepartmentId { get; set; }
        public Department ToDepartment { get; set; } = null!;

        public required int TransferredById { get; set; }
        public User TransferredByUser { get; set; } = null!;


        public TransferType TransferType { get; set; }


        public string Notes { get; set; } = string.Empty;

        public DateTime TransferredAt { get; set; }

        public bool IsActive { get; set; } //عند مين حاليا موجود باخر حركة





    }
}
