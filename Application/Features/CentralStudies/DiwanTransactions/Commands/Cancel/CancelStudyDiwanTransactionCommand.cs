using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.DiwanTransactions.Commands.Cancel
{
    public record CancelStudyDiwanTransactionCommand(int Id, string Reason) : IRequest<StudyDiwanTransactionDto>;
}
