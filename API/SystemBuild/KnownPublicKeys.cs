using System.Security.Cryptography;
using System.Text;

namespace API.SystemBuild
{
    /// <summary>
    /// مفاتيح نُشرت في المستودع (appsettings.json قبل 2026-10-28 وappsettings.Development.json الآن): معروفة لكل من يقرأ تاريخ git،
    /// فلا تصلح للإنتاج أبداً. نحفظ بصماتها (SHA-256) لا قيمها، ويرفض الإقلاع خارج بيئة التطوير أي مفتاح تطابق بصمته إحداها.
    /// أضف هنا بصمة أي مفتاح يُنشر في المستودع مستقبلاً.
    /// </summary>
    public static class KnownPublicKeys
    {
        private static readonly HashSet<string> Hashes = new(StringComparer.OrdinalIgnoreCase)
        {
            "25b67eaecdcc6c56608c5d50e082cc0a80a41c3fa58f764abf972aa191a9ffe0",   // مفتاح JWT القديم في appsettings.json
            "2bcbcdc3c5e8311529d740c454c12e89f2fc9a43976ee00b57d5b11e6e8deb97",   // مفتاح تشفير الأجهزة القديم في appsettings.json
            "a6046cbff1d7698b8117e831cd1b806f569607c9570b10c51e0371db0559eadf",   // مفتاح JWT للتطوير في appsettings.Development.json
            "fdd9d2bb3cca5c393c51e3fe4051034a7553eeb2f853659eb0a365041a212f0c"    // مفتاح تشفير الأجهزة للتطوير في appsettings.Development.json
        };

        public static bool IsPublic(string? key) =>
            !string.IsNullOrEmpty(key) && Hashes.Contains(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))));
    }
}
