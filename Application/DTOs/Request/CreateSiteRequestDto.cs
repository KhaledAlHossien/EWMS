namespace Application.DTOs.Request
{
    public class CreateSiteRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public int RegionId { get; set; }
    }
}
