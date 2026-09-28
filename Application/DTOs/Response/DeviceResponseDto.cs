namespace Application.DTOs.Response
{
    public class DeviceResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string SN { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
