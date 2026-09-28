using MediatR;

namespace Application.Features.Regions.Commands.Delete
{
    public record DeleteRegionCommand(int Id) : IRequest<bool>;
}
