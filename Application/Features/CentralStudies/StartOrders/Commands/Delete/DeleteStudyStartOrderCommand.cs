using MediatR;

namespace Application.Features.CentralStudies.StartOrders.Commands.Delete
{
    public record DeleteStudyStartOrderCommand(int Id) : IRequest<Unit>;
}
