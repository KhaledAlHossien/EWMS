using System.Text.RegularExpressions;
using Application.Features.Users;
using Application.Interfaces;
using Domain.Entities;

namespace Application.Features.Maintenance
{
    /// <summary>
    /// عميل طلب الصيانة (قرار المستخدم 2026-10-05): غالباً موظف في المؤسسة وأحياناً من خارجها.
    /// من يسجّل الطلب لا يتصفح الموظفين: يكتب اسم العميل الكامل أو رقمه الذاتي، فيُبحث بمطابقة تامة فقط
    /// (الرقم الذاتي، أو الاسم الكامل بعد توحيد الفراغات والهمزات والتاء المربوطة والألف المقصورة).
    /// لا نتيجة = عميل من خارج المؤسسة (اسم وهاتف يُكتبان يدوياً).
    /// </summary>
    public static class MaintenanceClients
    {
        public const int MaxMatches = 10;

        /// <summary>توحيد الاسم للمطابقة التامة: بلا تشكيل/تطويل، أشكال الهمزة → ا، ة → ه، ى → ي، فراغ واحد</summary>
        public static string NormalizeName(string? name)
        {
            var s = (name ?? string.Empty).Trim();
            s = Regex.Replace(s, "[ً-ٰٟـ]", "");
            s = Regex.Replace(s, "[أإآٱ]", "ا").Replace('ة', 'ه').Replace('ى', 'ي').Replace('ؤ', 'و').Replace('ئ', 'ي');
            return Regex.Replace(s, @"\s+", " ").ToLowerInvariant();
        }

        /// <summary>الموظفون النشطون المطابقون تماماً للرقم الذاتي أو للاسم الكامل</summary>
        public static async Task<List<User>> FindAsync(IUserService users, string query)
        {
            var q = (query ?? string.Empty).Trim();
            if (q.Length == 0) return [];

            var all = (await users.GetAllAsync()).Where(u => u.IsActive).ToList();

            var personalId = UserRules.NormalizePersonalIdNumber(q);
            var byId = all.Where(u => u.PersonalIdNumber != null && u.PersonalIdNumber == personalId).ToList();
            if (byId.Count > 0) return byId;

            var name = NormalizeName(q);
            return all.Where(u => NormalizeName(u.FullName) == name).Take(MaxMatches).ToList();
        }

        /// <summary>عميل موظف عند الحفظ: يجب أن يكون موجوداً ونشطاً (لا نعتمد على ما ترسله الواجهة)</summary>
        public static async Task<User> ResolveAsync(IUserService users, int clientUserId)
        {
            var user = await users.GetByIdAsync(clientUserId)
                ?? throw new KeyNotFoundException("العميل المحدد غير موجود");
            if (!user.IsActive) throw new InvalidOperationException("حساب العميل المحدد معطّل");
            return user;
        }
    }
}
