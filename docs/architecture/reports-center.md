# مركز التقارير

> التقارير الإحدى عشر والتصدير والسجل.
> نُقل هذا الملف حرفياً من CLAUDE.md (2026-10-28) ليبقى CLAUDE.md فهرساً قصيراً. المرجع الحاكم للقرارات: docs/adr/.

## Reports center (2026-10-12, user decisions; version stays 1.2.0)
- **One export permission per report** (preview + Excel + A4 print), whose boundary is the boundary of the report's page. **Every Excel download and print is logged**; the preview is not logged.
- **Reports (11).** Each report is one class in `Infrastructure/Reports/` implementing `IReport` (`Application/Interfaces/IReportService.cs`: `Key`, `Title`, `Group`, `Description`, `Permission`, `Landscape`, `GetFiltersAsync(viewer)`, `RunAsync(ReportRun)`). They are registered in `AddInfrastructureRigstrationServices`; the registration order is the display order.
  - الإجازات: `vacations` «سجل الإجازات» (`ExportVacationsReport`) and `vacation-usage` «استهلاك الإجازات الشهري» (from approved `VacationSegment`s). Boundary: `VacationScope.For(viewer)`, a public twin of `VacationAccess.CanView` (own + department/branch per the view permissions; SuperAdmin all).
  - الموظفون والهيكل: `users` and `structure`. Boundary: `StructureScope` (my branch; the whole organization for organization-wide viewers). The users report's «الدور» filter lists only the roles held by employees inside that boundary (fix 2026-10-13: a branch head saw every role).
  - الصيانة وقطع الغيار: `maintenance-requests` and `technicians`, with boundary `MaintenanceRules.ViewBoundary(viewer, <report permission>)` (own records, plus the department's with `ViewDepartmentMaintenance`). `spare-part-movements` and `spare-part-stock` use the `SparePartRules` boundary (my department's stock; SuperAdmin all).
  - الأجهزة والخريطة: `installations` (boundary `DeviceInventoryRules.ScopeFor`; filters for status and «لم يُتحقَّق منها منذ N يوم»), `sites` (the whole shared registry; installation counts within the installation scope) and `map-entries` (manual layers only, the visible fields become columns, file fields show their file names; boundary `MapLayerRules.EntryScopeFor`).
- **Filters are defined by the server** (`ReportFilters`: date / select / text / number, plus `DependsOn` for a child select such as department → branch).
  - Select options are built inside the viewer's boundary, and `ReportRules.Parse` rejects any value that is not an option, so nothing outside the boundary can be requested. The report query applies the boundary again, even when no filter is chosen.
  - `Parse` also returns the readable Arabic filters text used in the Excel header and the log.
- **Limits** (`ReportRules`): preview 50 rows, Excel 50,000, print 2,000. When the result is larger, the export is refused with «ضيّق الفلاتر». An empty result is refused with «لا توجد بيانات مطابقة».
- **Excel** is written by the single `Infrastructure/Files/ReportSpreadsheet` (ClosedXML):
  - right-to-left sheet; header rows with the title, the filters, who exported and when, and the row count;
  - frozen header row with auto-filter, number/date formats per column type, and a «المجموع» row for `Sum` columns;
  - A4 page setup with the header row repeated on each page.
- **Export log** `ReportExportLog` (UserId FK Restrict → user-delete guard; `BranchId` is a snapshot with no FK; `CreatedAt` in UTC). Page `GET Reports/Log` needs `ViewReportLog`; boundary = exports by users of my branch, or everything for organization-wide viewers.
- **The two older page exports now need their own permission and are logged too:**
  - `DeviceSites/Export` needs `ExportInstallationsReport` (logged as «التركيبات (تصدير صفحة التركيبات)»).
  - `AssignedTasks/Export` needs `ExportTasksReport` (key `tasks`).
  - Migration `Reports_Center` creates the log table, inserts these two permissions and grants them to every role that could export before (any device view permission / `ViewTaskBoard`). The other report permissions are seeded for SuperAdmin only and are granted from the Roles page (section «التقارير»).
- API `api/Reports` (policy `AnyReportExport` = any report permission; the handler checks the report's own permission): `Catalog`, `Definition/{key}`, `Preview/{key}`, `Export/{key}`, `Print/{key}`, plus `Log` and `LogOptions` (`ViewReportLog`). Code: `Application/Features/Reports/`.
- **UI** `features/reports/`:
  - `/reports`: report cards grouped by group.
  - `/reports/:key`: filters built from the definition and kept in the URL; the preview refreshes on every change; buttons for Excel and print.
  - `/reports/print/:key`: outside the layout, A4 (landscape for wide reports through an added `@page` rule), print columns only, «المجموع» row, page numbers in the page margin.
  - `/reports/log`.
  - Sidebar section «التقارير».
  - A failed file download now shows the server's Arabic message: `apiErrorInterceptor` reads JSON out of a Blob error.
  - `saveBlob` moved to `core/utils/download.ts`.
- **Adding a report:** write one class in `Infrastructure/Reports` and register it, add an `Export…Report` permission in `AppPermissions` (and to `AnyReportExport`), mirror it in UI `access.ts` (`REPORT_ACCESS`), and add it to the Roles page section «التقارير».
- Not built yet (phase 3): scheduled reports and generating large reports in the background (delivered as a notification with a download link, since the server has no e-mail).
- **الديوان (2026-10-28)**: تقريران جديدان `correspondence` و`correspondence-overdue` (`Infrastructure/Reports/DiwanReports.cs`) بصلاحيتي `ExportCorrespondenceReport` و`ExportOverdueCorrespondenceReport`، وحدّهما حدّ رؤية المعاملات؛ التفاصيل في `docs/architecture/diwan.md`.
