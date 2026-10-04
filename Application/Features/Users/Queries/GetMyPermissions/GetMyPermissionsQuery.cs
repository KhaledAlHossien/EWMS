using Application.Interfaces;
using MediatR;

namespace Application.Features.Users.Queries.GetMyPermissions
{
    /// <summary>
    /// صلاحيات دوري الآن (من قاعدة البيانات) — تستدعيها الواجهة لتحديث القوائم والأزرار
    /// بعد تعديل صلاحيات الدور دون إعادة تسجيل الدخول.
    /// </summary>
    public record GetMyPermissionsQuery : IRequest<MyPermissionsDto>;

    public class MyPermissionsDto
    {
        public List<string> Permissions { get; set; } = [];
    }

    public class GetMyPermissionsQueryHandler : IRequestHandler<GetMyPermissionsQuery, MyPermissionsDto>
    {
        private readonly ICurrentUserService _currentUser;
        private readonly IUserPermissionService _permissions;

        public GetMyPermissionsQueryHandler(ICurrentUserService currentUser, IUserPermissionService permissions)
        {
            _currentUser = currentUser;
            _permissions = permissions;
        }

        public async Task<MyPermissionsDto> Handle(GetMyPermissionsQuery request, CancellationToken ct) => new()
        {
            Permissions = (await _permissions.GetAsync(_currentUser.UserId)).OrderBy(p => p).ToList()
        };
    }
}
