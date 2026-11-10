using MediatR;

namespace Application.Features.CentralStudies.Stages.Commands.Delete
{
    public record DeleteStudyStageCommand(int Id) : IRequest<Unit>;
}
