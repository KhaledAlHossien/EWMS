using System.Security.Cryptography;
using System.Text;

namespace Application.Common
{
    /// <summary>
    /// بصمة رمز الدخول المحفوظة في UserTokens بدل الرمز نفسه: من يقرأ نسخة من القاعدة لا يحصل على جلسات صالحة.
    /// SHA-256 لنص الرمز (UTF-8) بست عشري صغير، 64 حرفاً — مطابقة لـ HASHBYTES('SHA2_256') في ترحيل User_Token_Hash.
    /// </summary>
    public static class TokenHash
    {
        public const int Length = 64;

        public static string Of(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    }
}
