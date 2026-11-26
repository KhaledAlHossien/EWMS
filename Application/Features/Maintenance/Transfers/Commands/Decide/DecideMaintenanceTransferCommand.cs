using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.Transfers.Commands.Decide
{
    public record DecideMaintenanceTransferCommand(int TransferId, DecideMaintenanceTransferDto Dto) : IRequest<MaintenanceTransferDto>;
}
