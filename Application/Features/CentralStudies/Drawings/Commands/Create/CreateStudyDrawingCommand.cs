using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Drawings.Commands.Create
{
    public record CreateStudyDrawingCommand(StudyDrawingFormDto Dto) : IRequest<StudyDrawingDto>;
}
