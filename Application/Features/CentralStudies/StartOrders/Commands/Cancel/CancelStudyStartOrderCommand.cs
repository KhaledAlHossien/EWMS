using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.StartOrders.Commands.Cancel
{
    public record CancelStudyStartOrderCommand(int Id, string Reason) : IRequest<StudyStartOrderDto>;
}
