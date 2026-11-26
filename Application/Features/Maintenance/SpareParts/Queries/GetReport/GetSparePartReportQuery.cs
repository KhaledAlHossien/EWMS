using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Queries.GetReport
{
    public record GetSparePartReportQuery(int? DepartmentId, DateTime? From, DateTime? To) : IRequest<SparePartReportDto>;
}
