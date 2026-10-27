using Application.Features.Projects;
using Application.Interfaces;
using Infrastructure.Persistence.Repositories;

namespace API.Background
{
    /// <summary>
    /// ينهي رفع وثائق المشاريع بعد اكتمال أجزائها (الحساب والفحص والحفظ لملف قد يبلغ عشرات الجيجابايتات يستغرق دقائق،
    /// فلا يجري داخل طلب المستخدم): يستيقظ بإشارة الإنهاء أو كل 30 ثانية، ويستأنف عند الإقلاع ما كان قيد الإنهاء.
    /// ويمسح كل ساعة جلسات الرفع المهجورة (48 ساعة) والمنتهية (24 ساعة) مع أجزائها المؤقتة.
    /// نسخة واحدة من الخادم تعمل على القاعدة (كبقية الخدمات الخلفية).
    /// </summary>
    public class ProjectUploadWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ProjectUploadSignal _signal;
        private readonly ILogger<ProjectUploadWorker> _logger;
        private DateTime _lastCleanupUtc = DateTime.MinValue;

        public ProjectUploadWorker(IServiceScopeFactory scopes, ProjectUploadSignal signal, ILogger<ProjectUploadWorker> logger)
        {
            _scopes = scopes;
            _signal = signal;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); } catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                await FinalizePendingAsync(stoppingToken);
                await CleanupAsync(stoppingToken);

                try { await _signal.WaitAsync(TimeSpan.FromSeconds(30), stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }

        private async Task FinalizePendingAsync(CancellationToken ct)
        {
            List<Guid> ids;
            try
            {
                using var scope = _scopes.CreateScope();
                ids = await scope.ServiceProvider.GetRequiredService<IProjectUploadService>().GetProcessingIdsAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "تعذّر قراءة عمليات الرفع التي تنتظر الإنهاء");
                return;
            }

            foreach (var id in ids)
            {
                if (ct.IsCancellationRequested) return;
                using var scope = _scopes.CreateScope();
                var uploads = scope.ServiceProvider.GetRequiredService<IProjectUploadService>();
                var files = scope.ServiceProvider.GetRequiredService<IProjectUploadFiles>();
                try
                {
                    var rejection = await scope.ServiceProvider.GetRequiredService<ProjectUploadFinalizer>().FinalizeAsync(id, ct);
                    if (rejection != null)
                    {
                        await uploads.SetStatusAsync(id, Domain.Entities.ProjectUploadStatus.Failed, rejection);
                        files.Delete(id);
                        _logger.LogInformation("رُفض رفع وثيقة {Id}: {Reason}", id, rejection);
                    }
                    else
                        _logger.LogInformation("اكتمل رفع وثيقة مشروع {Id}", id);
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "تعذّر إنهاء رفع وثيقة {Id}", id);
                    var message = ex is InvalidOperationException ? ex.Message : "تعذّر حفظ الملف على الخادم — أعد الرفع، وإن تكرر فأبلغ مدير النظام";
                    await uploads.SetStatusAsync(id, Domain.Entities.ProjectUploadStatus.Failed, message);
                    files.Delete(id);
                }
            }
        }

        private async Task CleanupAsync(CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            if (now - _lastCleanupUtc < TimeSpan.FromHours(1)) return;
            _lastCleanupUtc = now;

            try
            {
                using var scope = _scopes.CreateScope();
                var uploads = scope.ServiceProvider.GetRequiredService<IProjectUploadService>();
                var files = scope.ServiceProvider.GetRequiredService<IProjectUploadFiles>();

                var stuck = await uploads.FailStuckAsync(now - ProjectUploadRules.ProcessingStuckAfter);
                var expired = await uploads.GetExpiredIdsAsync(now - ProjectUploadRules.AbandonedAfter, now - ProjectUploadRules.FinishedKeptFor);
                foreach (var id in expired)
                {
                    ct.ThrowIfCancellationRequested();
                    files.Delete(id);
                    await uploads.DeleteAsync(id);
                }
                if (stuck > 0 || expired.Count > 0)
                    _logger.LogInformation("تنظيف رفع الوثائق: {Stuck} متعثرة، {Expired} مهجورة أو منتهية", stuck, expired.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "تعذّر تنظيف جلسات رفع الوثائق");
            }
        }
    }
}
