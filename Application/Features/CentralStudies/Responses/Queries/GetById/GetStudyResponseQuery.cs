using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Responses.Queries.GetById
{
    public record GetStudyResponseQuery(int Id) : IRequest<StudyResponseDto>;
}
