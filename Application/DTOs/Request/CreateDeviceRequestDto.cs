namespace Application.DTOs.Request
{
    public class CreateDeviceRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string SN { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
