using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class ProjectFile
    {
        public int Id { get; set; }

        public required int ProjectId { get; set; }

        public Project Project { get; set; } = null!;

        public required int UploadedById { get; set; }
        public User User { get; set; } = null!;

        public string FileName { get; set; } = string.Empty;
        public required string FilePath { get; set; }
        public string Notes { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; }

    }
}
