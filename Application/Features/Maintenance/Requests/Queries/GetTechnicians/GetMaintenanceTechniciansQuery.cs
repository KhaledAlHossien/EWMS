using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.Requests.Queries.GetTechnicians
{
    /// <summary>الفنيون الذين لهم طلبات ضمن نطاقي (لقائمة فلتر "الفني")</summary>
    public record GetMaintenanceTechniciansQuery : IRequest<List<TechnicianOptionDto>>;
}
