using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.StartOrders.Commands.Submit
{
    public record SubmitStudyStartOrderCommand(int Id) : IRequest<StudyStartOrderDto>;
}
