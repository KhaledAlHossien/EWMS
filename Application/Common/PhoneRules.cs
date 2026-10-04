using System.Text.RegularExpressions;

namespace Application.Common;

/// <summary>
/// رقم هاتف سوري بعد حذف الفراغات والشرطات: جوال 09XXXXXXXX، أو أرضي 0 + رمز المحافظة + الرقم (9–10 خانات)،
/// ويُقبل بالصيغة الدولية ‎+963 / 00963 بدل الصفر. مطابق لـ core/utils/phone.ts في الواجهة — عدّلهما معاً.
/// يستخدمه هاتف العميل في الصيانة ورقم تواصل المستخدم.
/// </summary>
public static class PhoneRules
{
    private static readonly Regex PhonePattern = new(@"^(?:0|\+963|00963)[1-9]\d{7,8}$", RegexOptions.Compiled);

    public static string Normalize(string? phone) => Regex.Replace(phone ?? "", @"[\s\-()]", "");

    /// <summary>فارغ = مقبول (الحقل اختياري)</summary>
    public static bool IsValid(string? phone)
    {
        var value = Normalize(phone);
        return value.Length == 0 || PhonePattern.IsMatch(value);
    }

    /// <summary>للحفظ: بلا فراغات، والفارغ = null</summary>
    public static string? ToStored(string? phone)
    {
        var value = Normalize(phone);
        return value.Length == 0 ? null : value;
    }
}
