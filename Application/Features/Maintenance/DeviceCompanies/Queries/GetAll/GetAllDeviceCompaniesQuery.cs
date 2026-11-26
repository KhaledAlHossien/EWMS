using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.DeviceCompanies.Queries.GetAll
{
    public record GetAllDeviceCompaniesQuery : IRequest<List<DeviceCompanyResponseDto>>;
}
