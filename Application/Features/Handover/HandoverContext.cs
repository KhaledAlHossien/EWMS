using Application.Common;
using Application.DTOs.Response;
using Application.Features.Contracts;
using Application.Features.Schedules;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using static Application.Features.Handover.HandoverSupport;

namespace Application.Features.Handover
{
    /// <summary>
    /// الاستلام والتسليم
    /// تبعيات المعالجات المشتركة وما تشترك فيه من تحميل وحراسة (تركيب لا وراثة): كل حالة استخدام معالج مستقل يأخذ هذا السياق.
    /// </summary>
    public sealed class HandoverContext(IHandoverService handovers, IContractService contracts, IProjectService projects, IDiwanService diwan,
        IUserService users, IUserPermissionService permissions, INotificationService notifications)
    {
        public IHandoverService Handovers { get; } = handovers;
        public IContractService Contracts { get; } = contracts;
        public IProjectService Projects { get; } = projects;
        public IDiwanService Diwan { get; } = diwan;
        public IUserService Users { get; } = users;
        public IUserPermissionService Permissions { get; } = permissions;
        public INotificationService Notifications { get; } = notifications;

        public Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(Users, Permissions);

        /// <summary>المحضر بكل ما تحته بعد التحقق من أن مشروعه ضمن حدّ المستخدم وأنه يملك صلاحية استلام</summary>
        public async Task<HandoverRecord> LoadAsync(Viewer viewer, int id) => Guard(viewer, await Handovers.GetByIdAsync(id));

        public async Task<(HandoverRecord Record, HandoverNote Note)> LoadNoteAsync(Viewer viewer, int noteId)
        {
            var record = Guard(viewer, await Handovers.GetByNoteAsync(noteId));
            return (record, record.Notes.First(n => n.Id == noteId));
        }

        private static HandoverRecord Guard(Viewer viewer, HandoverRecord? record)
        {
            if (record == null) throw new KeyNotFoundException("محضر الاستلام غير موجود");
            HandoverRules.EnsureCanView(viewer, record.Project);
            return record;
        }

        public async Task<HandoverDetailDto> DetailAsync(Viewer viewer, int id)
        {
            var record = await Handovers.GetByIdAsync(id) ?? throw new KeyNotFoundException("محضر الاستلام غير موجود");
            return ToDetail(record, viewer, DateTime.Today);
        }

        /// <summary>طلب الاستلام يمرّ عبر معاملة واردة من الديوان (اختياري): من فرع المشروع ويراها المستخدم</summary>
        public async Task<Correspondence?> ResolveCorrespondenceAsync(Viewer viewer, int? id, int branchId)
        {
            if (id is not int correspondenceId) return null;
            var c = await Diwan.GetByIdAsync(correspondenceId) ?? throw new KeyNotFoundException("المعاملة غير موجودة");
            Application.Features.Diwan.DiwanRules.EnsureView(viewer, c);
            if (c.BranchId != branchId) throw new InvalidOperationException("المعاملة من فرع آخر غير فرع المشروع");
            if (c.Direction != CorrespondenceDirection.Incoming) throw new InvalidOperationException("يُربط طلب الاستلام بمعاملة واردة");
            return c;
        }

        /// <summary>إصدار وثيقة مثبَّت: من إضبارة المشروع نفسه (وإلا خطأ)</summary>
        public async Task<ProjectDocumentVersion?> ResolveVersionAsync(int? versionId, int projectId)
        {
            if (versionId is not int id) return null;
            var version = await Handovers.GetVersionAsync(id) ?? throw new KeyNotFoundException("إصدار الوثيقة غير موجود");
            if (version.Document.ProjectId != projectId) throw new InvalidOperationException("الوثيقة من مشروع آخر");
            return version;
        }

        /// <summary>حاملو الصلاحية في فرع المشروع ممن يرونه بحدّه</summary>
        public Task<List<int>> HoldersAsync(Project project, string permission) =>
            ScheduleNotifier.HoldersAsync(Permissions, project, permission);

        /// <summary>إشعار لكل مستلم (بلا تكرار وبلا المنفِّذ) يفتح المحضر من الواجهة</summary>
        public async Task NotifyAsync(IEnumerable<int> userIds, int exceptUserId, HandoverRecord record, NotificationType type, string title, string message)
        {
            var now = DateTime.UtcNow;
            var rows = userIds.Distinct().Where(id => id != exceptUserId).Select(id => new Notification
            {
                UserId = id, Title = title, Message = message, Type = type,
                RelatedEntityType = AuditEntities.Handover, RelatedEntityId = record.Id, CreatedAt = now
            }).ToList();
            if (rows.Count > 0) await Notifications.AddRangeAsync(rows);
        }

        public static string Label(HandoverRecord record) => $"{HandoverRules.Number(record.Id, record.CreatedAt)} ({HandoverRules.KindAr(record.Kind)})";
    }
}
