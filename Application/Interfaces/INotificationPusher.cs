using Domain.Entities;

namespace Application.Interfaces
{
    /// <summary>
    /// دفع الإشعارات لحظياً للمستخدمين المتصلين (التنفيذ في طبقة API عبر SignalR).
    /// يُستدعى بعد حفظ الإشعارات في قاعدة البيانات — فشله لا يُفشل العملية الأصلية.
    /// </summary>
    public interface INotificationPusher
    {
        Task PushAsync(IReadOnlyCollection<Notification> notifications);
    }
}
