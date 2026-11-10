using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Stages.Commands.Update
{
    public record UpdateStudyStageCommand(int Id, StudyStageFormDto Dto) : IRequest<ProjectStageDto>;
}
