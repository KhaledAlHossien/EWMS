using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Stage
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public required int ProjectId { get; set; }
      //  public Project Project { get; set; } = null!;
        public string Description { get; set; } = string.Empty;
    }
}
