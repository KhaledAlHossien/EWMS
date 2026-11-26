using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.Requests.Queries.GetAssignees
{
    /// <summary>
    /// الموظفون الذين يمكن نقل طلب/مهمة إليهم: موظفو قسم رئيس القسم،
    /// وللسوبر ادمن موظفو القسم المحدد (أو كل من له قسم).
    /// </summary>
    public record GetMaintenanceAssigneesQuery(int? DepartmentId) : IRequest<List<TechnicianOptionDto>>;
}
