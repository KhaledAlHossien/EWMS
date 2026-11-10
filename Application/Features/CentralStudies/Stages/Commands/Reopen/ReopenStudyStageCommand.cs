using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Stages.Commands.Reopen
{
    public record ReopenStudyStageCommand(int Id) : IRequest<ProjectStageDto>;
}
