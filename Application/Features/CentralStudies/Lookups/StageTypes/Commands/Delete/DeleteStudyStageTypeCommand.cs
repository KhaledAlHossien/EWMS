using MediatR;

namespace Application.Features.CentralStudies.Lookups.StageTypes.Commands.Delete
{
    public record DeleteStudyStageTypeCommand(int Id) : IRequest<Unit>;
}
