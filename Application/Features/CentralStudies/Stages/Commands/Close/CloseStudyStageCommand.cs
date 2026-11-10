using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Stages.Commands.Close
{
    public record CloseStudyStageCommand(int Id) : IRequest<ProjectStageDto>;
}
