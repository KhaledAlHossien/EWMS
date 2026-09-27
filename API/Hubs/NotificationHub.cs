using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace API.Hubs
{
    /// <summary>
    /// قناة الإشعارات اللحظية. الاتصال يتطلب JWT صالحاً (يُمرَّر كـ access_token في الـ query).
    /// كل مستخدم يُعرَّف بـ NameIdentifier (UserId) — لذلك نرسل عبر Clients.User(userId).
    /// الخادم فقط يرسل؛ لا توجد دوال يستدعيها العميل.
    /// </summary>
    [Authorize]
    public class NotificationHub : Hub
    {
        public const string Path = "/hubs/notifications";
        public const string ReceiveMethod = "notification";
    }
}
