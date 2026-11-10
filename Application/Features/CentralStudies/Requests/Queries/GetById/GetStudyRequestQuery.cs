using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Requests.Queries.GetById
{
    public record GetStudyRequestQuery(int Id) : IRequest<StudyRequestDto>;
}
