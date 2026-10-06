namespace Domain.Entities.Maintenance
{
    public class DeviceType // نوع الجهاز للصيانة
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        // حد «إصلاحه صار أغلى من استبداله»: تنبيه حين تبلغ تكلفة قطع جهاز من هذا النوع على مدى عمره هذا المبلغ (null = بلا تنبيه)
        public decimal? ReplacementCostThreshold { get; set; }
    }
}
