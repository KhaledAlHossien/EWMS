using Domain.Entities;

namespace Application.Interfaces
{
    /// <summary>
    /// المستخدم الحالي. من التوكن رقمه فقط؛ دوره ومكانه (فرع/قسم/مكتب) يُقرآن من قاعدة البيانات دائماً،
    /// كي يسري تغيير الدور أو النقل فوراً لا بعد انتهاء الجلسة.
    /// </summary>
    public interface ICurrentUserService
    {
        int UserId { get; }
        bool IsAuthenticated { get; }

        /// <summary>المستخدم الحالي كما هو الآن في قاعدة البيانات (مع دوره)، مخزَّناً طوال الطلب</summary>
        Task<User> GetUserAsync();
    }
}
