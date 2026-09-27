namespace Application.Features.Users
{
    /// <summary>
    /// التبعية التنظيمية المطلوبة لكل دور:
    /// SuperAdmin لا يتبع لفرع/قسم/مكتب، رئيس الفرع يتبع لفرع فقط،
    /// رئيس القسم يتبع لفرع وقسم، وباقي الأدوار (الموظف والأدوار المخصصة) لفرع وقسم ومكتب.
    /// نفس القاعدة مطبّقة في الواجهة (users-page) — عدّلهما معاً.
    /// </summary>
    public readonly record struct UserPlacement(bool NeedsBranch, bool NeedsDepartment, bool NeedsOffice)
    {
        public static UserPlacement For(string roleName) => roleName switch
        {
            var r when r.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) => new(false, false, false),
            var r when r.Equals("BranchManager", StringComparison.OrdinalIgnoreCase) => new(true, false, false),
            var r when r.Equals("Manager", StringComparison.OrdinalIgnoreCase) => new(true, true, false),
            _ => new(true, true, true)
        };
    }
}
