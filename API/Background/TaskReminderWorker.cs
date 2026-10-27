using Application.Features.AssignedTasks;
using Application.Features.MapLayers;
using Application.Features.Schedules;
using Application.Features.ToDoLists;
using Application.Interfaces;

namespace API.Background
{
    /// <summary>
    /// ينشئ المهام الدورية المستحقة (AssignedTaskRecurrenceRunner) ثم يفحص مواعيد المهام ويرسل التذكيرات (AssignedTaskReminders). أول فحص بعد 20 ثانية من الإقلاع،
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
                    // المهام الدورية أولاً: ما أُنشئ اليوم يدخل فحص التذكيرات في الدورة نفسها
                    using var recurring = _scopes.CreateScope();
                    var made = await AssignedTaskRecurrenceRunner.RunAsync(
                        recurring.ServiceProvider.GetRequiredService<IAssignedTaskPlanningService>(),
                        recurring.ServiceProvider.GetRequiredService<AssignedTaskCreator>(),
                        recurring.ServiceProvider.GetRequiredService<IUserPermissionService>(),
                        recurring.ServiceProvider.GetRequiredService<INotificationService>(),
                        DateTime.Now);
                    if (made > 0) _logger.LogInformation("أُنشئت {Count} مهام من المهام الدورية", made);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "تعذّر تنفيذ المهام الدورية — تُعاد المحاولة في الدورة التالية");
                }

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

                try
                {
                    // تذكيرات بنود «مفكرتي» (صاحب القائمة وحده)
                    using var todo = _scopes.CreateScope();
                    var todoSent = await ToDoReminders.RunAsync(
                        todo.ServiceProvider.GetRequiredService<IToDoListService>(),
                        todo.ServiceProvider.GetRequiredService<INotificationService>(),
                        DateTime.Now);
                    if (todoSent > 0) _logger.LogInformation("أُرسلت {Count} تذكيرات لبنود المفكرة", todoSent);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "تعذّر فحص تذكيرات المفكرة — تُعاد المحاولة في الدورة التالية");
                }

                try
                {
                    // بنود جداول المشاريع المعتمدة التي تجاوزت نهايتها (مرشَّحة للتأخر): تنبيه لا قرار
                    using var overdue = _scopes.CreateScope();
                    var flagged = await ScheduleOverdueReminders.RunAsync(
                        overdue.ServiceProvider.GetRequiredService<IScheduleChangeService>(),
                        overdue.ServiceProvider.GetRequiredService<IUserPermissionService>(),
                        overdue.ServiceProvider.GetRequiredService<INotificationService>(),
                        DateTime.Now);
                    if (flagged > 0) _logger.LogInformation("نُبّه عن {Count} بنداً تجاوز نهايته في جداول المشاريع", flagged);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "تعذّر فحص بنود الجداول المتأخرة — تُعاد المحاولة في الدورة التالية");
                }

                try
                {
                    // ملفات حقول الطبقات المرفوعة ولم يُحفظ سجلها خلال 24 ساعة
                    using var files = _scopes.CreateScope();
                    var removed = await MapEntryFiles.CleanupAsync(files.ServiceProvider.GetRequiredService<IMapEntryFileService>(), DateTime.UtcNow);
                    if (removed > 0) _logger.LogInformation("حُذف {Count} ملفاً معلّقاً من حقول الطبقات", removed);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "تعذّر حذف الملفات المعلّقة — تُعاد المحاولة في الدورة التالية");
                }

                try
                {
                    // مجلد المرفقات: حذف ما لم يعد له سجل (مرة كل 6 ساعات على الأكثر، وفي وضع FileSystem فقط)
                    using var store = _scopes.CreateScope();
                    var swept = await store.ServiceProvider.GetRequiredService<IFileStoreSweeper>().SweepAsync(stoppingToken);
                    if (swept > 0) _logger.LogInformation("حُذف {Count} ملفاً لا سجل له من مجلد المرفقات", swept);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "تعذّر تنظيف مجلد المرفقات — تُعاد المحاولة لاحقاً");
                }

                try { await Task.Delay(_interval, stoppingToken); } catch (OperationCanceledException) { return; }
            }
        }
    }
}
