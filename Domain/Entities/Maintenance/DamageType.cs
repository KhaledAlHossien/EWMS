namespace Domain.Entities.Maintenance
{
    public class DamageType //نوع العطل
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
    }
}
