using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Drawings.Commands.Review
{
    public record ReviewStudyDrawingCommand(int Id, StudyDrawingReviewDto Dto) : IRequest<StudyDrawingDto>;
}
