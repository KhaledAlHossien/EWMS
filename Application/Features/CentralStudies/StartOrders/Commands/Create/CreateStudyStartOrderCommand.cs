using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.StartOrders.Commands.Create
{
    public record CreateStudyStartOrderCommand(StudyStartOrderFormDto Dto) : IRequest<StudyStartOrderDto>;
}
