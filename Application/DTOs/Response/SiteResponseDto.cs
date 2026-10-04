namespace Application.DTOs.Response
{
    public class SiteResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string GovernorateCode { get; set; } = string.Empty;
        public string GovernorateName { get; set; } = string.Empty;
    }
}
