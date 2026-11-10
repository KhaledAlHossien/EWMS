using MediatR;

namespace Application.Features.CentralStudies.Drawings.Commands.Delete
{
    public record DeleteStudyDrawingCommand(int Id) : IRequest<Unit>;
}
