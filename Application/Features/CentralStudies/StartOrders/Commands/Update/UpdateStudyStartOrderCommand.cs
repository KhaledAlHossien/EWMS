using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.StartOrders.Commands.Update
{
    public record UpdateStudyStartOrderCommand(int Id, StudyStartOrderFormDto Dto) : IRequest<StudyStartOrderDto>;
}
