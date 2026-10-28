using Application.Common;
using Application.Features.Diwan;
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
        private readonly IProjectService _projects;
        private readonly IDiwanService _diwan;
        private readonly IStudyService _studies;

        public TaskLinkResolver(IMaintenanceRequestService maintenance, IVacationService vacations, ISiteService sites, IProjectService projects, IDiwanService diwan, IStudyService studies)
        {
            _maintenance = maintenance;
            _vacations = vacations;
            _sites = sites;
            _projects = projects;
            _diwan = diwan;
            _studies = studies;
        }

        public sealed record Described(bool Exists, bool Available, string Label);

        public static string TypeAr(TaskLinkType type) => type switch
        {
            TaskLinkType.MaintenanceRequest => "طلب صيانة",
            TaskLinkType.Vacation => "إجازة",
            TaskLinkType.Project => "مشروع",
            TaskLinkType.Correspondence => "معاملة",
            TaskLinkType.Study => "دراسة",
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

        /// <summary>
        /// رقم معاملة الديوان من نصها: «IN-2026-00012» رقم تسلسل داخل الفرع لا معرّف السجل، فيُبحث عنه ضمن ما يراه المستخدم
        /// (ويجب أن يطابق رقماً واحداً). الأرقام وحدها تُعامَل معرّفاً كبقية الأنواع.
        /// </summary>
        public async Task<int?> ResolveCorrespondenceReferenceAsync(Viewer viewer, string? reference)
        {
            var text = (reference ?? string.Empty).Trim();
            if (!text.StartsWith("IN-", StringComparison.OrdinalIgnoreCase) && !text.StartsWith("OUT-", StringComparison.OrdinalIgnoreCase))
                return ParseReference(text);

            var (items, _) = await _diwan.SearchAsync(DiwanRules.Visible(viewer),
                new DiwanFilter(null, null, null, null, null, null, text, null, null, false, null, DateTime.Today), 1, 5);
            var match = items.Where(c => string.Equals(DiwanRules.Number(c.Direction, c.Year, c.Sequence), text, StringComparison.OrdinalIgnoreCase)).ToList();
            return match.Count == 1 ? match[0].Id : null;
        }

        private static bool CanViewStudy(Viewer viewer, Domain.Entities.StudyOrder study)
        {
            try { Features.Studies.StudyRules.EnsureCanView(viewer, study.Project); return true; }
            catch (UnauthorizedAccessException) { return false; }
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

                case TaskLinkType.Project:
                    var project = await _projects.GetByIdAsync(id);
                    if (project == null) return new(false, false, string.Empty);
                    return Features.Projects.ProjectRules.CanView(viewer, project)
                        ? new(true, true, $"{Features.Projects.ProjectRules.Number(project.Id, project.CreatedAt)} — {project.Name}")
                        : new(true, false, string.Empty);

                case TaskLinkType.Correspondence:
                    var correspondence = await _diwan.GetByIdAsync(id);
                    if (correspondence == null) return new(false, false, string.Empty);
                    return DiwanRules.CanView(viewer, correspondence)
                        ? new(true, true, $"{DiwanRules.Number(correspondence.Direction, correspondence.Year, correspondence.Sequence) ?? "مسودة"} — {correspondence.Subject}")
                        : new(true, false, string.Empty);

                case TaskLinkType.Study:
                    var study = await _studies.GetByIdAsync(id);
                    if (study == null) return new(false, false, string.Empty);
                    return CanViewStudy(viewer, study)
                        ? new(true, true, $"{Features.Studies.StudyRules.Number(study.Id, study.CreatedAt)} — {study.Title}")
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
