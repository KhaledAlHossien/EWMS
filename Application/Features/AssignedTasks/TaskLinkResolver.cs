using Application.Common;
using Application.Features.Maintenance;
using Application.Features.Vacations;
using Application.Interfaces;
using Domain.Enums;

namespace Application.Features.AssignedTasks
{
    /// <summary>
    /// ربط المهام بسجلات من أنظمة أخرى (قرار المستخدم 2026-10-08): الربط والعرض مشروطان بأن يحق للمستخدم عرض السجل
    /// بحدود صلاحياته في ذلك النظام (طلب صيانة: حدّ ViewMaintenanceRequests، إجازة: VacationAccess.CanView، موقع: أي صلاحية أجهزة).
    /// من لا يحق له عرض السجل يرى أن هناك رابطاً دون أي بيانات عنه.
    /// </summary>
    public class TaskLinkResolver
    {
        private readonly IMaintenanceRequestService _maintenance;
        private readonly IVacationService _vacations;
        private readonly ISiteService _sites;

        public TaskLinkResolver(IMaintenanceRequestService maintenance, IVacationService vacations, ISiteService sites)
        {
            _maintenance = maintenance;
            _vacations = vacations;
            _sites = sites;
        }

        public sealed record Described(bool Exists, bool Available, string Label);

        public static string TypeAr(TaskLinkType type) => type switch
        {
            TaskLinkType.MaintenanceRequest => "طلب صيانة",
            TaskLinkType.Vacation => "إجازة",
            _ => "موقع"
        };

        public static bool TryParseType(string? value, out TaskLinkType type) =>
            Enum.TryParse(value, true, out type) && Enum.IsDefined(type);

        /// <summary>رقم السجل من نصه: «MR-2026-00012» أو «VAC-2026-00005» أو «12» ← الأرقام الأخيرة</summary>
        public static int? ParseReference(string? reference)
        {
            if (string.IsNullOrWhiteSpace(reference)) return null;
            var digits = new string(reference.Trim().Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
            return int.TryParse(digits, out var id) && id > 0 ? id : null;
        }

        public async Task<Described> DescribeAsync(Viewer viewer, TaskLinkType type, int id)
        {
            switch (type)
            {
                case TaskLinkType.MaintenanceRequest:
                    var request = await _maintenance.GetByIdAsync(id);
                    if (request == null) return new(false, false, string.Empty);
                    var boundary = MaintenanceRules.ViewBoundary(viewer, "ViewMaintenanceRequests");
                    return MaintenanceRules.In(boundary, request)
                        ? new(true, true, $"{MaintenanceRules.RequestNumber(request.Id, request.CreatedAt)} — {request.DeviceMaintenance?.Name}")
                        : new(true, false, string.Empty);

                case TaskLinkType.Vacation:
                    var vacation = await _vacations.GetWithDetailsAsync(id);
                    if (vacation == null) return new(false, false, string.Empty);
                    return VacationAccess.CanView(viewer, vacation)
                        ? new(true, true, $"{VacationRules.RequestNumber(vacation.Id, vacation.CreatedAt)} — {vacation.User?.FullName}")
                        : new(true, false, string.Empty);

                default:
                    var site = await _sites.GetByIdAsync(id);
                    if (site == null) return new(false, false, string.Empty);
                    return AppPermissions.AnyOf["AnyDeviceView"].Any(viewer.Has) || viewer.IsSuperAdmin
                        ? new(true, true, site.Name)
                        : new(true, false, string.Empty);
            }
        }
    }
}
