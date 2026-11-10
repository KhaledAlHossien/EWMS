using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.ArchitecturalVersions.Commands.Delete
{
    public record DeleteArchitecturalVersionCommand(int Id) : IRequest<StudyDrawingDto>;
}
