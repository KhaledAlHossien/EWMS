using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.DeviceCompanies.Queries.GetById
{
    public record GetDeviceCompanyByIdQuery(int Id) : IRequest<DeviceCompanyResponseDto>;
}
