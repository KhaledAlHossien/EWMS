using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.ArchitecturalVersions.Commands.Update
{
    public record UpdateArchitecturalVersionCommand(int Id, ArchitecturalVersionFormDto Dto) : IRequest<StudyDrawingDto>;
}
