namespace Application.DTOs.Request
{
    public class CreateDeviceSiteRequestDto
    {
        public int DeviceId { get; set; }
        public int SiteId { get; set; }
        public string Ip { get; set; } = string.Empty;
        public string SubnetMask { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Pass { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string SN { get; set; } = string.Empty;
        public string InstallLocation { get; set; } = string.Empty;
    }
}
