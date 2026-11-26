using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Dashboard.Queries.GetOverview
{
    /// <summary>
    /// نطاق الداشبوردات (قرار المستخدم 2026-09-28):
    /// - SuperAdmin يطّلع على كل شيء (نظرة عامة + أي فرع/قسم/مكتب).
    /// - ViewBranchDashboard: فرعه وأقسامه ومكاتبه. ViewDepartmentDashboard: قسمه ومكاتبه. ViewOfficeDashboard: مكتبه (Role-Permission، 2026-10-03).
    /// - كل مستخدم: داشبورده الشخصي (Me).
    /// عند عدم تمرير id يُستخدم نطاق المستخدم نفسه.
    /// </summary>
    public record GetOverviewDashboardQuery : IRequest<OverviewDashboardDto>;
}
