using Application.Common;

namespace Application.Features.DeviceInventory
{
    /// <summary>
    /// المحافظة (المنطقة الثابتة) تُشتق من إحداثيات الموقع في الخادم دائماً (قرار المستخدم 2026-10-03):
    /// لا يُرسلها العميل ولا يمكن أن تتعارض مع النقطة. نقطة خارج كل المحافظات (بحر/حدود) تُرفض برسالة واضحة.
    /// </summary>
    internal static class GovernorateResolver
    {
        public static string CodeFor(double? latitude, double? longitude)
        {
            if (latitude is not double lat || longitude is not double lng)
                throw new ArgumentException("حدّد موقع النقطة على الخريطة");

            return Governorates.Locate(lat, lng)?.Code
                ?? throw new ArgumentException("النقطة خارج حدود المحافظات السورية — حدّد نقطة داخل إحدى المحافظات");
        }
    }
}
