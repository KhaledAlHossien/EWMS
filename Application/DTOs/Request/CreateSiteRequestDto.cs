namespace Application.DTOs.Request
{
    public class CreateSiteRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int RegionId { get; set; }
    }
}
