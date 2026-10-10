using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.AssignedTasks
{
    /// <summary>
    /// ينفّذ المهام الدورية التي حان موعدها (يستدعيه العامل الخلفي): ينشئ مهمة واحدة لكل تكرار بصلاحيات صاحبه الحالية،
    /// ثم يقدّم NextRunDate. الفائت أثناء توقف الخادم لا يُعوَّض (مهمة واحدة فقط). أي فشل يوقف ذلك التكرار برسالة ويُشعر صاحبه
    /// ولا يوقف بقية التكرارات. إنشاء المهمة وتقديم الموعد في معاملة واحدة مع قفل (RunScheduledAsync): كل موعد يُنفَّذ مرة واحدة
    /// حتى لو تعطّل الخادم بينهما أو عملت أكثر من نسخة منه.
    /// </summary>
    public static class AssignedTaskRecurrenceRunner
    {
        public static NewTaskSpec SpecFor(AssignedTaskRecurrence r, DateTime today)
        {
            var t = r.Template;
            var days = r.DueAfterDays ?? t.DefaultDueDays;
            return new NewTaskSpec(
                t.Title, t.Description, (int)t.Priority, days == null ? null : today.AddDays(days.Value),
                r.TargetType.ToString(), r.TargetId, null,
                t.Items.OrderBy(i => i.SortOrder).Select(i => i.Text).ToList(),
                RecurringName: t.Name);
        }

        public static async Task<int> RunAsync(
            IAssignedTaskPlanningService planning, AssignedTaskCreator creator, IUserPermissionService permissions,
            INotificationService notifications, DateTime localNow)
        {
            var today = localNow.Date;
            var created = 0;
            foreach (var due in await planning.GetDueRecurrencesAsync(today))
            {
                try
                {
                    var ran = await planning.RunScheduledAsync(due.Id, due.NextRunDate!.Value, async r =>
                    {
                        var owner = r.OwnerUser;
                        if (!owner.IsActive) throw new InvalidOperationException("حساب صاحب المهمة الدورية غير فعّال");

                        var viewer = new Viewer(owner, await permissions.GetAsync(owner.Id));
                        var task = await creator.CreateAsync(viewer, SpecFor(r, today));
                        r.LastRunAt = DateTime.UtcNow;
                        r.LastTaskId = task.Id;
                        r.LastError = null;
                        r.NextRunDate = TaskPlanningRules.AdvanceAfter(r, today);
                        if (r.NextRunDate == null) r.IsActive = false;      // انتهت المدة
                    });
                    if (ran) created++;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or KeyNotFoundException or ArgumentException)
                {
                    // المحاولة رُجعت كاملة؛ يوقف التكرار ويُشعر صاحبه (مرة واحدة: إن أوقفته نسخة أخرى فلا إشعار ثانٍ)
                    var error = ex.Message.Length > 450 ? ex.Message[..450] : ex.Message;
                    if (await planning.StopRecurrenceAsync(due.Id, error))
                        await notifications.AddAsync(new Notification
                        {
                            UserId = due.OwnerUserId,
                            Title = "توقفت مهمة دورية",
                            Message = $"تعذّر إنشاء المهمة الدورية «{due.Template.Name}»: {error}",
                            Type = NotificationType.TaskRecurrenceStopped,
                            RelatedEntityType = TaskPlanningRules.RecurrenceEntityType,
                            RelatedEntityId = due.Id,
                            CreatedAt = DateTime.UtcNow
                        });
                }
            }
            return created;
        }
    }
}
