using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Tasks
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public required int StageId { get; set; }
        public Stage Stage { get; set; } = null!;
        public string Description { get; set; } = string.Empty;
    }
}
