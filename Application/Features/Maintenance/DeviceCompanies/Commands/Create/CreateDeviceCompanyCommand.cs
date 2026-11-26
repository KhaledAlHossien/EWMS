using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.DeviceCompanies.Commands.Create
{
    public record CreateDeviceCompanyCommand(DeviceCompanyRequestDto Dto) : IRequest<DeviceCompanyResponseDto>;
}
