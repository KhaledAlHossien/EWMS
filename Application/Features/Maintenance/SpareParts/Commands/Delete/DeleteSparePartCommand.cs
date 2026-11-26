using MediatR;

namespace Application.Features.Maintenance.SpareParts.Commands.Delete
{
    public record DeleteSparePartCommand(int Id) : IRequest<Unit>;
}
