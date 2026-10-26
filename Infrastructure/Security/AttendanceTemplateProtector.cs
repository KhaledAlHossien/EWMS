using System.Security.Cryptography;
using Application.Interfaces;
using Infrastructure.Attendance;

namespace Infrastructure.Security
{
    /// <summary>
    /// تشفير القوالب الحيوية: بايت الإصدار (1) | nonce[12] | tag[16] | المشفَّر — بالمفتاح Attendance:TemplateKey (32 بايت Base64).
    /// </summary>
    public sealed class AttendanceTemplateProtector : IAttendanceTemplateProtector
    {
        private const byte Version = 1;
        private readonly byte[]? _key;

        public AttendanceTemplateProtector(AttendanceSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.TemplateKey)) return;
            try { _key = Convert.FromBase64String(settings.TemplateKey); } catch (FormatException) { _key = null; }
            if (_key is { Length: not 32 }) _key = null;
        }

        private byte[] Key => _key ?? throw new InvalidOperationException(
            "مفتاح تشفير قوالب البصمة غير مضبوط (Attendance:TemplateKey: 32 بايت بصيغة Base64)");

        public byte[] Protect(byte[] plain)
        {
            var nonce = RandomNumberGenerator.GetBytes(12);
            var cipher = new byte[plain.Length];
            var tag = new byte[16];
            using (var aes = new AesGcm(Key, 16))
                aes.Encrypt(nonce, plain, cipher, tag);
            return [Version, .. nonce, .. tag, .. cipher];
        }

        public byte[] Unprotect(byte[] stored)
        {
            if (stored.Length < 29 || stored[0] != Version)
                throw new InvalidOperationException("صيغة قالب مشفّر غير معروفة");
            var plain = new byte[stored.Length - 29];
            try
            {
                using var aes = new AesGcm(Key, 16);
                aes.Decrypt(stored[1..13], stored[29..], stored[13..29], plain);
            }
            catch (CryptographicException)
            {
                throw new InvalidOperationException("تعذّر فك قالب البصمة — مفتاح التشفير في الإعدادات لا يطابق المفتاح الذي حُفظ به");
            }
            return plain;
        }
    }
}
