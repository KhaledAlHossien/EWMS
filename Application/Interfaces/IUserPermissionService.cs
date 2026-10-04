using Domain.Entities;

namespace Application.Interfaces
{
    /// <summary>
    /// صلاحيات المستخدم كما هي الآن في قاعدة البيانات (دوره ← RolePermissions)، لا من التوكن.
    /// الـ Policy تفتح العملية، والـ handler يستخدم هذه الخدمة ليحدد السجلات حسب حدّ كل صلاحية.
    /// </summary>
    public interface IUserPermissionService
    {
        /// <summary>صلاحيات المستخدم (تُخزَّن مؤقتاً خلال الطلب الواحد)</summary>
        Task<IReadOnlySet<string>> GetAsync(int userId);

        Task<bool> HasAsync(int userId, string permission);

        /// <summary>
        /// المستخدمون النشطون الذين يملك دورهم الصلاحية، مقيّدين بالوحدة المعطاة (null = بلا قيد).
        /// exactUnit: وحدة المستخدم نفسها هي الوحدة المعطاة (لا وحدة أدنى منها) — مثلاً من يتبع للقسم مباشرة بلا مكتب.
        /// </summary>
        Task<List<User>> GetUsersWithPermissionAsync(
            string permission, int? branchId = null, int? departmentId = null, int? officeId = null, bool exactUnit = false);
    }
}
