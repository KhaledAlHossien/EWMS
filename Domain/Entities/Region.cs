namespace Domain.Entities
{
    public class Region
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        // الإحداثيات (خريطة سوريا) — null للسجلات القديمة قبل إضافتها
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
