using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Drawings.Commands.Update
{
    public record UpdateStudyDrawingCommand(int Id, StudyDrawingFormDto Dto) : IRequest<StudyDrawingDto>;
}
