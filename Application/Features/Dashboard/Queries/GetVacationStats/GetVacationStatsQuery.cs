using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Dashboard.Queries.GetVacationStats
{
    /// <summary>
    /// صفحة إحصائيات الإجازات المستقلة: ViewBranchVacations → فرعه، ViewDepartmentVacations → قسمه،
    /// و SuperAdmin يرى المؤسسة كلها أو يختار فرعاً.
    /// </summary>
    public record GetVacationStatsQuery(int? BranchId) : IRequest<VacationStatsDto>;
}
