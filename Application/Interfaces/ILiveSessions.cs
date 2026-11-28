namespace Application.Interfaces
{
    /// <summary>
    /// الاتصالات اللحظية المفتوحة (قناة الإشعارات). فحص الرمز يجري عند فتح الاتصال فقط،
    /// لذلك يُقطع الاتصال المفتوح عند إبطال جلسته.
    /// </summary>
    public interface ILiveSessions
    {
        /// <summary>كل اتصالات المستخدم (أُبطلت جلساته كلها)</summary>
        void CloseUser(int userId);

        /// <summary>اتصالات جلسة واحدة ببصمة رمزها (تسجيل الخروج)</summary>
        void CloseToken(string tokenHash);
    }
}
