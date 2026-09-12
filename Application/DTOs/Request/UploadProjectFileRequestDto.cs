namespace Application.DTOs.Request
{
    public class UploadProjectFileRequestDto
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }
}
