using Infrastructure.Files;
using Infrastructure.Persistence.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace API.SystemBuild
{
    /// <summary>فحص صحة الخدمة (/health): ينجح إن أمكن الاتصال بقاعدة البيانات</summary>
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly DataContext _context;

        public DatabaseHealthCheck(DataContext context) => _context = context;

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            await _context.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("تعذّر الاتصال بقاعدة البيانات");
    }

    /// <summary>فحص صحة مجلد المرفقات (/health) في وضع FileSystem: يكتب ملف اختبار صغيراً ويحذفه — انقطاع المشاركة يظهر فوراً</summary>
    public class FileStoreHealthCheck : IHealthCheck
    {
        private readonly FileStorageSettings _settings;

        public FileStoreHealthCheck(FileStorageSettings settings) => _settings = settings;

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(
                _settings.Root is string root
                    ? (FileStore.CanWrite(root) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("تعذّرت الكتابة في مجلد المرفقات"))
                    : _settings.UseFileSystem
                        ? HealthCheckResult.Unhealthy("مجلد المرفقات غير مضبوط")
                        // وضع Database بلا مجلد: النظام يعمل، لكن مرفقات فرع الدراسات (تُحفظ في المجلد دائماً) لا تُرفع
                        : HealthCheckResult.Degraded("مرفقات فرع الدراسات تحتاج FileStorage:Root"));
    }
}
