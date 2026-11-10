using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.StartOrders.Queries.GetById
{
    public record GetStudyStartOrderQuery(int Id) : IRequest<StudyStartOrderDto>;
}
