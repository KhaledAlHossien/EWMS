namespace Domain.Entities
{
    public class Site
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // إحداثيات أو عنوان الموقع (اختياري - قد تُنسخ من خرائط جوجل)
        public string Location { get; set; } = string.Empty;

        public required int RegionId { get; set; }
        public Region Region { get; set; } = null!;
    }
}
