using Application.Common;
using Application.DTOs.Request;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory.Installations.Queries.Export
{
    public class ExportInstallationsQueryHandler : IRequestHandler<ExportInstallationsQuery, byte[]>
    {
        private readonly IDeviceSiteService _installations;
        private readonly IDeviceService _devices;
        private readonly ISiteService _sites;
        private readonly IDeviceSpreadsheet _spreadsheet;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly IReportLogService _reportLog;

        public ExportInstallationsQueryHandler(IDeviceSiteService installations, IDeviceService devices, ISiteService sites, IDeviceSpreadsheet spreadsheet, IUserService users, IUserPermissionService permissions, IReportLogService reportLog)
        {
            _installations = installations;
            _devices = devices;
            _sites = sites;
            _spreadsheet = spreadsheet;
            _users = users;
            _permissions = permissions;
            _reportLog = reportLog;
        }

        /// <summary>تصدير صفحة التركيبات (صلاحية ExportInstallationsReport) — يُسجَّل في سجل التقارير كتقرير «التركيبات»</summary>
        public async Task<byte[]> Handle(ExportInstallationsQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var rows = await _installations.ExportAsync(request.Filter, ScopeFor(viewer), MaxExportRows);
            var content = _spreadsheet.ExportInstallations(rows);
            await _reportLog.AddAsync(Reports.ReportRules.Log(viewer, "installations", "التركيبات (تصدير صفحة التركيبات)",
                ReportFormat.Excel, await ExportFiltersTextAsync(request.Filter), rows.Count));
            return content;
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);

        private async Task<string> ExportFiltersTextAsync(InstallationFilterDto f)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(f.Q)) parts.Add($"بحث: {f.Q.Trim()}");
            if (!string.IsNullOrWhiteSpace(f.GovernorateCode)) parts.Add($"المحافظة: {Governorates.NameOf(f.GovernorateCode)}");
            if (f.SiteId is int siteId) parts.Add($"الموقع: {(await _sites.GetByIdAsync(siteId))?.Name ?? "#" + siteId}");
            if (f.DeviceId is int deviceId) parts.Add($"الجهاز: {(await _devices.GetByIdAsync(deviceId))?.Name ?? "#" + deviceId}");
            if (f.Status is int status && Enum.IsDefined((InstallationStatus)status)) parts.Add($"الحالة: {StatusAr((InstallationStatus)status)}");
            return parts.Count == 0 ? "بلا فلاتر (كل ما تسمح به صلاحيتك)" : string.Join(" · ", parts);
        }
    }
}
