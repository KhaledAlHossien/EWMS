namespace Application.Interfaces
{
    public interface IUserSignatureService
    {
        /// <summary>صورة التوقيع الحالي (Data URL) أو null إن لم يكن للمستخدم توقيع حالي</summary>
        Task<string?> GetAsync(int userId);

        /// <summary>رقم نسخة التوقيع الحالي — يُحفظ مع القرار لحظة التوقيع</summary>
        Task<int?> GetCurrentIdAsync(int userId);

        /// <summary>صورة نسخة محددة (للطباعة: التوقيع كما كان وقت القرار)</summary>
        Task<string?> GetImageAsync(int? signatureId);

        /// <summary>نسخة جديدة تصبح الحالية، أو لا نسخة حالية إن كانت الصورة null — النسخ القديمة تبقى</summary>
        Task SetAsync(int userId, string? image);
    }
}
