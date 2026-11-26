using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.DeviceCompanies.Commands.Update
{
    public record UpdateDeviceCompanyCommand(int Id, DeviceCompanyRequestDto Dto) : IRequest<DeviceCompanyResponseDto>;
}
