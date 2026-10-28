using System.Security.Cryptography;
using System.Text;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Security
{
    /// <summary>
    /// تشفير كلمات سر الأجهزة (قرار المستخدم 2026-10-05: المفتاح في ملف الإعدادات DeviceInventory:PasswordKey — 32 بايت Base64).
    /// الصيغة المحفوظة: enc:v1:Base64(nonce[12] | tag[16] | ciphertext). ضياع المفتاح = ضياع كلمات السر، فانسخه احتياطياً
    /// وانقله إلى متغيرات بيئة الخادم قبل النشر (مثل مفتاح JWT).
    /// </summary>
    public class DevicePasswordProtector : IDevicePasswordProtector
    {
        private const string Prefix = "enc:v1:";
        private readonly byte[]? _key;
        private readonly List<byte[]> _oldKeys = [];

        public DevicePasswordProtector(IConfiguration configuration)
        {
            var configured = configuration["DeviceInventory:PasswordKey"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                try { _key = Convert.FromBase64String(configured); } catch (FormatException) { _key = null; }
                if (_key is { Length: not 32 }) _key = null;
            }

            // مفاتيح قديمة لتدوير المفتاح: يفكّ بها ما شُفّر بها، ويُعاد تشفيره بالحالي عند الإقلاع (DbSeeder.EncryptDevicePasswordsAsync)
            foreach (var old in configuration.GetSection("DeviceInventory:OldPasswordKeys").GetChildren().Select(c => c.Value))
            {
                if (string.IsNullOrWhiteSpace(old)) continue;
                try { var bytes = Convert.FromBase64String(old); if (bytes.Length == 32) _oldKeys.Add(bytes); } catch (FormatException) { }
            }
        }

        private byte[] Key => _key ?? throw new InvalidOperationException(
            "مفتاح تشفير كلمات سر الأجهزة غير مضبوط (DeviceInventory:PasswordKey: 32 بايت بصيغة Base64)");

        public bool IsProtected(string stored) => stored.StartsWith(Prefix, StringComparison.Ordinal);

        public string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return string.Empty;
            var nonce = RandomNumberGenerator.GetBytes(12);
            var data = Encoding.UTF8.GetBytes(plain);
            var cipher = new byte[data.Length];
            var tag = new byte[16];
            using (var aes = new AesGcm(Key, 16))
                aes.Encrypt(nonce, data, cipher, tag);
            return Prefix + Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
        }

        public string Unprotect(string stored)
        {
            if (string.IsNullOrEmpty(stored) || !IsProtected(stored)) return stored;   // نص قديم قبل الترحيل
            var payload = Convert.FromBase64String(stored[Prefix.Length..]);
            var (nonce, tag, cipher) = (payload[..12], payload[12..28], payload[28..]);

            // المفتاح الحالي أولاً ثم القديمة (وسم GCM يوثّق أي المفاتيح صحيح، فلا التباس)
            foreach (var key in new[] { Key }.Concat(_oldKeys))
            {
                var plain = new byte[cipher.Length];
                try
                {
                    using var aes = new AesGcm(key, 16);
                    aes.Decrypt(nonce, cipher, tag, plain);
                    return Encoding.UTF8.GetString(plain);
                }
                catch (CryptographicException) { }
            }
            throw new InvalidOperationException("تعذّر فك كلمة السر — مفتاح التشفير في الإعدادات لا يطابق المفتاح الذي حُفظت به");
        }

        public bool NeedsRotation(string stored)
        {
            if (_oldKeys.Count == 0 || string.IsNullOrEmpty(stored) || !IsProtected(stored)) return false;
            var payload = Convert.FromBase64String(stored[Prefix.Length..]);
            var (nonce, tag, cipher) = (payload[..12], payload[12..28], payload[28..]);
            try
            {
                using var aes = new AesGcm(Key, 16);
                aes.Decrypt(nonce, cipher, tag, new byte[cipher.Length]);
                return false;
            }
            catch (CryptographicException) { return true; }
        }
    }
}
