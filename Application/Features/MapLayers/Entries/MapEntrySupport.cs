using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using static Application.Features.MapLayers.MapLayerRules;

namespace Application.Features.MapLayers.Entries
{
    /// <summary>الدوال المشتركة بين عمليات هذه الميزة</summary>
    public class MapEntrySupport
    {
        private readonly IMapEntryService _entries;
        private readonly IDepartmentService _departments;

        public MapEntrySupport(IMapEntryService entries, IDepartmentService departments)
        {
            _entries = entries;
            _departments = departments;
        }

        public async Task<MapEntry> LoadAsync(int id, Viewer viewer)
        {
            var entry = await _entries.GetByIdAsync(id) ?? throw new KeyNotFoundException("السجل غير موجود");
            EnsureEntry(viewer, entry);
            return entry;
        }

        /// <summary>
        /// القسم المالك للسجل داخل فرع الطبقة: القسم المطلوب إن كان مسموحاً (نظرة المؤسسة، أو ViewBranchMapEntries في فرعه)،
        /// وإلا قسم المستخدم إن كان من فرع الطبقة، وإلا بلا قسم (سجل على مستوى الفرع).
        /// </summary>
        public async Task<int?> DepartmentAsync(Viewer viewer, MapLayer layer, int? requestedDepartmentId)
        {
            if (requestedDepartmentId is int departmentId && departmentId != viewer.User.DepartmentId)
            {
                var department = await _departments.GetByIdAsync(departmentId) ?? throw new KeyNotFoundException("القسم غير موجود");
                if (department.BranchId != layer.BranchId)
                    throw new InvalidOperationException("القسم المختار لا يتبع فرع الطبقة");
                var allowed = viewer.IsOrganizationWide
                    || (viewer.Has(AppPermissions.ViewBranchMapEntries) && OrganizationRole.InBranch(viewer.User, layer.BranchId));
                if (!allowed) throw new UnauthorizedAccessException("لا يمكنك تسجيل سجل باسم قسم آخر");
                return department.Id;
            }
            return OrganizationRole.InBranch(viewer.User, layer.BranchId) ? viewer.User.DepartmentId : null;
        }
    }
}
