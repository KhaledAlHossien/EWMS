using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Users.Queries.GetMyPermissions
{
    /// <summary>
    /// صلاحيات دوري الآن (من قاعدة البيانات) — تستدعيها الواجهة لتحديث القوائم والأزرار
    /// بعد تعديل صلاحيات الدور دون إعادة تسجيل الدخول.
    /// </summary>
    public record GetMyPermissionsQuery : IRequest<MyPermissionsDto>;
}
