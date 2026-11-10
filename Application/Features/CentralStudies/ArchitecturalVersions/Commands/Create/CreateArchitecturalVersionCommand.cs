using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.ArchitecturalVersions.Commands.Create
{
    public record CreateArchitecturalVersionCommand(int DrawingId, ArchitecturalVersionFormDto Dto) : IRequest<StudyDrawingDto>;
}
