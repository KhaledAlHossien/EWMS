using MediatR;

namespace Application.Features.Maintenance.DamageTypes.Commands.Delete
{
    public record DeleteDamageTypeCommand(int Id) : IRequest<Unit>;
}
