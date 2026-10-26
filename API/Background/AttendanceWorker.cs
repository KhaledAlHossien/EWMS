using Application.Features.Attendance;
using Application.Interfaces;

namespace API.Background
{
    /// <summary>
    /// خدمة الحضور الخلفية (تعمل مع Attendance:Enabled فقط)، كل 30 ثانية:
    /// - مزامنة الأجهزة المعلَّمة «تحتاج مزامنة»، وتسوية كل الأجهزة كل 30 دقيقة (تلتقط أي تغيير لم يُعلِّم جهازه).
    /// - اكتشاف الانقطاع والعودة وإشعارهما — بعد أن يعمل الخادم مدة الانقطاع نفسها، كي لا تُعدّ كل الأجهزة منقطعة عند الإقلاع.
    /// - كل ساعة: حذف صور البصم الأقدم من مدة الاحتفاظ، وإنهاء مهلة الأوامر بلا رد، وتنظيف سجل الأوامر القديم.
    /// أي خطأ يُسجَّل ويُعاد في الدورة التالية دون إيقاف الخادم.
    /// </summary>
    public class AttendanceWorker : BackgroundService
    {
        private static readonly TimeSpan Every = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan FullSyncEvery = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan CleanupEvery = TimeSpan.FromHours(1);

        private readonly IServiceScopeFactory _scopes;
        private readonly IAttendanceSettings _settings;
        private readonly ILogger<AttendanceWorker> _logger;

        public AttendanceWorker(IServiceScopeFactory scopes, IAttendanceSettings settings, ILogger<AttendanceWorker> logger)
        {
            _scopes = scopes;
            _settings = settings;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_settings.Enabled) return;
            var started = DateTime.UtcNow;
            var lastFullSync = DateTime.MinValue;
            var lastCleanup = DateTime.UtcNow;   // أول تنظيف بعد ساعة من الإقلاع

            try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); } catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;
                await StepAsync("مزامنة أجهزة البصمة", async services =>
                {
                    var full = now - lastFullSync >= FullSyncEvery;
                    var queued = await services.GetRequiredService<IAttendanceSyncService>().SyncAsync(full, stoppingToken);
                    if (full) lastFullSync = now;
                    if (queued > 0) _logger.LogInformation("وُضع {Count} أمر مزامنة على أجهزة البصمة", queued);
                });

                if (now - started >= TimeSpan.FromMinutes(_settings.OfflineMinutes))
                    await StepAsync("فحص اتصال أجهزة البصمة", async services =>
                    {
                        var changes = await services.GetRequiredService<IAttendanceSyncService>()
                            .DetectStatusChangesAsync(now.AddMinutes(-_settings.OfflineMinutes), stoppingToken);
                        var permissions = services.GetRequiredService<IUserPermissionService>();
                        var notifications = services.GetRequiredService<INotificationService>();
                        var areas = services.GetRequiredService<IAttendanceAreaService>();
                        foreach (var change in changes)
                        {
                            if (change.Online)
                                await AttendanceNotifier.DeviceOnlineAsync(permissions, notifications, areas, change.DeviceId, change.Name, change.AreaId);
                            else
                                await AttendanceNotifier.DeviceOfflineAsync(permissions, notifications, areas, change.DeviceId, change.Name, change.AreaId, _settings.OfflineMinutes);
                        }
                    });

                if (now - lastCleanup >= CleanupEvery)
                    await StepAsync("تنظيف بيانات الحضور", async services =>
                    {
                        var photosBefore = now.AddHours(_settings.TimeZoneHours).Date.AddDays(-_settings.PhotoRetentionDays);
                        var removed = await services.GetRequiredService<IAttendanceSyncService>().CleanupAsync(photosBefore, stoppingToken);
                        lastCleanup = now;
                        if (removed > 0) _logger.LogInformation("حُذفت {Count} صورة بصم انتهت مدة الاحتفاظ بها", removed);
                    });

                try { await Task.Delay(Every, stoppingToken); } catch (OperationCanceledException) { return; }
            }
        }

        private async Task StepAsync(string name, Func<IServiceProvider, Task> step)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await step(scope.ServiceProvider);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "تعذّر تنفيذ «{Step}» — تُعاد المحاولة في الدورة التالية", name);
            }
        }
    }
}
