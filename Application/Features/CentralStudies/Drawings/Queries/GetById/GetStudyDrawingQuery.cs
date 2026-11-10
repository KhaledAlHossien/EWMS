using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Drawings.Queries.GetById
{
    public record GetStudyDrawingQuery(int Id) : IRequest<StudyDrawingDto>;
}
