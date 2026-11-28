using MediatR;

namespace Application.Features.CentralStudies.DiwanTransactions.Commands.Delete
{
    public record DeleteStudyDiwanTransactionCommand(int Id) : IRequest<Unit>;
}
