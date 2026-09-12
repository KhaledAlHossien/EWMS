using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class ProjectAssignments
    {
        public int Id { get; set; }
        public required int ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        public required int AssignedUserId { get; set; }
        public User AssignedUser { get; set; } = null!;
        public required int AssignedByUserId { get; set; }

        public User AssignedByUser { get; set; } = null!;

        public AssignmentStatus Status { get; set; }

        public DateTime AssignedAt { get; set; }

    }
}
