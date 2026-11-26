using Application.Common;
using Application.DTOs.Request;
using Application.Interfaces;
using Domain.Entities;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory.Installations
{
    /// <summary>الدوال المشتركة بين عمليات هذه الميزة</summary>
    public class InstallationSupport
    {
        private readonly IDeviceSiteService _installations;
        private readonly IDeviceService _devices;
        private readonly ISiteService _sites;
        private readonly IDepartmentService _departments;

        public InstallationSupport(IDeviceSiteService installations, IDeviceService devices, ISiteService sites, IDepartmentService departments)
        {
            _installations = installations;
            _devices = devices;
            _sites = sites;
            _departments = departments;
        }

        public async Task<DeviceSite> LoadAsync(int id) =>
            await _installations.GetByIdAsync(id) ?? throw new KeyNotFoundException("التركيب غير موجود");

        /// <summary>التركيب ضمن حدّ المستخدم (قسمه/فرعه/المؤسسة) وإلا 403</summary>
        public async Task<DeviceSite> LoadInScopeAsync(int id, Viewer viewer)
        {
            var installation = await LoadAsync(id);
            EnsureInScope(viewer, installation);
            return installation;
        }

        /// <summary>
        /// الوحدة المالكة لتركيب جديد أو منقول: القسم المطلوب إن كان مسموحاً (نظرة المؤسسة: أي قسم،
        /// ViewBranchDevices: أقسام فرعه)، وإلا قسم المستخدم وفرعه.
        /// </summary>
        public async Task<(int BranchId, int? DepartmentId)> UnitAsync(Viewer viewer, int? requestedDepartmentId)
        {
            if (requestedDepartmentId is int departmentId && departmentId != viewer.User.DepartmentId)
            {
                var department = await _departments.GetByIdAsync(departmentId) ?? throw new KeyNotFoundException("القسم غير موجود");
                var allowed = viewer.IsOrganizationWide
                    || (viewer.Has(AppPermissions.ViewBranchDevices) && OrganizationRole.InBranch(viewer.User, department.BranchId));
                if (!allowed) throw new UnauthorizedAccessException("لا يمكنك تسجيل تركيب باسم قسم آخر");
                return (department.BranchId, department.Id);
            }
            if (viewer.User.BranchId is int branchId) return (branchId, viewer.User.DepartmentId);
            throw new InvalidOperationException(viewer.IsOrganizationWide
                ? "حسابك لا يتبع فرعاً — اختر القسم الذي يتبع له التركيب"
                : "حسابك غير مرتبط بفرع أو قسم، فلا يمكن تسجيل تركيب باسمه");
        }

        /// <summary>الحقول المنظّفة كما تُحفظ (بلا كلمة السر — تُعالج منفصلة)</summary>
        public static void Apply(InstallationRequestDto dto, DeviceSite i)
        {
            i.DeviceId = dto.DeviceId;
            i.SiteId = dto.SiteId;
            i.Ip = Clean(dto.Ip);
            i.SubnetMask = Clean(dto.SubnetMask);
            i.Gateway = Clean(dto.Gateway);
            i.UserName = Clean(dto.UserName);
            i.Note = Clean(dto.Note);
            i.SN = Clean(dto.SN);
            i.InstallLocation = Clean(dto.InstallLocation);
            i.MacAddress = NetworkRules.NormalizeMac(dto.MacAddress);
            i.Port = Clean(dto.Port);
            i.Vlan = dto.Vlan;
            i.Firmware = Clean(dto.Firmware);
            i.InstallDate = dto.InstallDate?.Date;
            i.Status = (InstallationStatus)dto.Status;
        }

        public async Task<(Device Device, Site Site)> ReferencesAsync(int deviceId, int siteId) =>
            (await _devices.GetByIdAsync(deviceId) ?? throw new KeyNotFoundException("الجهاز المحدد غير موجود"),
             await _sites.GetByIdAsync(siteId) ?? throw new KeyNotFoundException("الموقع المحدد غير موجود"));
    }
}
