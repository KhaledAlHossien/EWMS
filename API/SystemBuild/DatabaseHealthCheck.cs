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
}
