using Application.Features.AssignedTasks;
using Application.Interfaces;

namespace API.Background
{
    /// <summary>
    /// يفحص مواعيد المهام دورياً ويرسل التذكيرات (انظر AssignedTaskReminders). أول فحص بعد 20 ثانية من الإقلاع،
    /// ثم كل TaskReminders:IntervalMinutes دقيقة (الافتراضي 10). أي خطأ يُسجَّل ويُعاد الفحص في الدورة التالية دون إيقاف الخادم.
    /// </summary>
    public class TaskReminderWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<TaskReminderWorker> _logger;
        private readonly TimeSpan _interval;

        public TaskReminderWorker(IServiceScopeFactory scopes, ILogger<TaskReminderWorker> logger, IConfiguration configuration)
        {
            _scopes = scopes;
            _logger = logger;
            _interval = TimeSpan.FromMinutes(Math.Clamp(configuration.GetValue("TaskReminders:IntervalMinutes", 10), 1, 1440));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); } catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var sent = await AssignedTaskReminders.RunAsync(
                        scope.ServiceProvider.GetRequiredService<IAssignedTaskService>(),
                        scope.ServiceProvider.GetRequiredService<IUserPermissionService>(),
                        scope.ServiceProvider.GetRequiredService<INotificationService>(),
                        DateTime.Now);
                    if (sent > 0) _logger.LogInformation("أُرسلت {Count} تذكيرات لمواعيد المهام", sent);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "تعذّر فحص تذكيرات المهام — تُعاد المحاولة في الدورة التالية");
                }

                try { await Task.Delay(_interval, stoppingToken); } catch (OperationCanceledException) { return; }
            }
        }
    }
}
