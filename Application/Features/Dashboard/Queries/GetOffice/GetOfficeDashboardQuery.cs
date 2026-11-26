using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Dashboard.Queries.GetOffice
{
    public record GetOfficeDashboardQuery(int? OfficeId) : IRequest<OfficeDashboardDto>;
}
