using Application.Common;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.Schedules
{
    /// <summary>
    /// إشعارات الجدول الزمني (على نمط VacationNotifier): المستلمون حاملو صلاحية العملية في فرع المشروع ممن يرون المشروع بحدّه،
    /// فلا يصل إشعار عن مشروع لا يراه صاحبه. الإشعار يفتح المشروع (RelatedEntityType = Project).
    /// </summary>
    public static class ScheduleNotifier
    {
        /// <summary>حاملو الصلاحية في فرع المشروع ممن يرونه (المشروع محمَّل بمشاركيه ومستخدميهم)</summary>
        public static async Task<List<int>> HoldersAsync(IUserPermissionService permissions, Project project, string permission,
            Dictionary<int, IReadOnlySet<string>>? cache = null)
        {
            var result = new List<int>();
            foreach (var user in (await permissions.GetUsersWithPermissionAsync(permission, project.BranchId)).DistinctBy(u => u.Id))
            {
                IReadOnlySet<string> granted;
                if (cache != null && cache.TryGetValue(user.Id, out var known)) granted = known;
                else
                {
                    granted = await permissions.GetAsync(user.Id);
                    if (cache != null) cache[user.Id] = granted;
                }
                if (ProjectRules.CanView(new Viewer(user, granted), project)) result.Add(user.Id);
            }
            return result;
        }

        public static async Task SendAsync(INotificationService notifications, IEnumerable<int> userIds, int exceptUserId,
            Project project, NotificationType type, string title, string message)
        {
            var items = userIds.Distinct().Where(id => id != exceptUserId).Select(id => new Notification
            {
                UserId = id,
                Title = title,
                Message = message,
                Type = type,
                RelatedEntityType = AuditEntities.Project,
                RelatedEntityId = project.Id,
                CreatedAt = DateTime.UtcNow
            }).ToList();
            if (items.Count > 0) await notifications.AddRangeAsync(items);
        }

        public static string Label(Project project) => $"{ProjectRules.Number(project.Id, project.CreatedAt)} — {project.Name}";
    }

    /// <summary>
    /// تنبيه «مرشَّح للتأخر» (قرار المرحلة 1: النظام ينبّه ولا يقرّر): بند في جدول معتمد تجاوز نهايته ولم يكتمل ولم يُوقف
    /// يُنبَّه عنه مسؤولُه وحاملو RecordScheduleDelay في الفرع ممن يرون المشروع، مرة لكل نهاية. التنبيه يطلب المراجعة:
    /// يحدّث الإنجاز أو يسجّل التأخير. تمديد النهاية يعيد السماح بتنبيه جديد. آمن للتكرار.
    /// </summary>
    public static class ScheduleOverdueReminders
    {
        public const int BatchSize = 500;

        public static async Task<int> RunAsync(IScheduleChangeService changes, IUserPermissionService permissions,
            INotificationService notifications, DateTime localNow)
        {
            var today = localNow.Date;
            var tasks = await changes.GetOverdueToNotifyAsync(today, BatchSize);
            if (tasks.Count == 0) return 0;

            var holders = new Dictionary<int, List<int>>();     // مفتاحه المشروع: الحاملون يُجلبون مرة لكل مشروع في الدورة
            var granted = new Dictionary<int, IReadOnlySet<string>>();
            var sent = 0;

            foreach (var task in tasks)
            {
                var project = task.Schedule.Project;
                if (!holders.TryGetValue(project.Id, out var recipients))
                    holders[project.Id] = recipients = await ScheduleNotifier.HoldersAsync(permissions, project, AppPermissions.RecordScheduleDelay, granted);

                var ids = new List<int>(recipients);
                if (task.AssigneeUserId is int assignee) ids.Add(assignee);

                var days = (today - task.PlannedEnd.Date).Days;
                await ScheduleNotifier.SendAsync(notifications, ids, exceptUserId: -1, project, NotificationType.ScheduleTaskOverdue,
                    "بند تجاوز نهايته",
                    $"«{task.Name}» في المشروع {ScheduleNotifier.Label(project)} تجاوز نهايته المخططة ({task.PlannedEnd:yyyy/MM/dd}) بـ {days} {(days == 1 ? "يوم" : "أيام")} " +
                    "ولم يكتمل — راجع حالته: حدّث الإنجاز أو سجّل التأخير");
                task.OverdueNotifiedFor = task.PlannedEnd.Date;
                sent++;
            }

            await changes.SaveChangesAsync();
            return sent;
        }
    }
}
