namespace Application.DTOs.Response
{
    public class ProjectFileResponseDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public int UploadedById { get; set; }
        public string UploadedByName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
    }
}
