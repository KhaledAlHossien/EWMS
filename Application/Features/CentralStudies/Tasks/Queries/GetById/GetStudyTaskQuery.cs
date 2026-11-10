using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Tasks.Queries.GetById
{
    public record GetStudyTaskQuery(int Id) : IRequest<StageTaskDto>;
}
