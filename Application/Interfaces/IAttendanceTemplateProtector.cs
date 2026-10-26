namespace Application.Interfaces
{
    /// <summary>
    /// تشفير القوالب الحيوية وصور تسجيل الوجه (AES-GCM بالمفتاح Attendance:TemplateKey). ضياع المفتاح لا يوقف الأجهزة
    /// (كل جهاز يحتفظ بنسخته) لكنه يعني سحب القوالب من الأجهزة من جديد — فاحفظه احتياطياً مع مفتاح كلمات سر الأجهزة.
    /// </summary>
    public interface IAttendanceTemplateProtector
    {
        byte[] Protect(byte[] plain);
        byte[] Unprotect(byte[] stored);
    }
}
