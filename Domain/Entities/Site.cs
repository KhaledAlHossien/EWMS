namespace Domain.Entities
{
    public class Site
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // الإحداثيات (خريطة سوريا) — null للسجلات القديمة قبل إضافتها
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        /// <summary>
        /// المحافظة (المنطقة الثابتة): رمزها SYxx من حدود الخريطة. يحددها الخادم من الإحداثيات عند كل حفظ
        /// (Application.Common.Governorates) ولا يُرسلها العميل. فارغة فقط لسجلات قديمة بلا إحداثيات.
        /// </summary>
        public string GovernorateCode { get; set; } = string.Empty;

        // مسؤول الموقع: للتواصل عند العطل أو الزيارة
        public string ContactName { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
        /// <summary>الجهة التي يتبع لها الموقع (فرع أو قسم أو جهة خارجية) — نص حر</summary>
        public string ResponsibleParty { get; set; } = string.Empty;

        public byte[] RowVersion { get; set; } = [];
    }
}
