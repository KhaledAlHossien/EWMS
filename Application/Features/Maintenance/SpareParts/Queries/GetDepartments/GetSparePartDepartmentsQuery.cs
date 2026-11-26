using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Queries.GetDepartments
{
    public record GetSparePartDepartmentsQuery : IRequest<List<NamedRefDto>>;
}
