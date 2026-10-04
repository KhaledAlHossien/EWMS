namespace Application.DTOs.Response
{
    public class DeviceSiteResponseDto
    {
        public int Id { get; set; }

        public int DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceModel { get; set; } = string.Empty;

        public int SiteId { get; set; }
        public string SiteName { get; set; } = string.Empty;
        public string GovernorateCode { get; set; } = string.Empty;
        public string GovernorateName { get; set; } = string.Empty;

        public string Ip { get; set; } = string.Empty;
        public string SubnetMask { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Pass { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string SN { get; set; } = string.Empty;
        public string InstallLocation { get; set; } = string.Empty;
    }
}
