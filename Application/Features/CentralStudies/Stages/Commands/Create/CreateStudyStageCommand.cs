using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Stages.Commands.Create
{
    public record CreateStudyStageCommand(StudyStageFormDto Dto) : IRequest<ProjectStageDto>;
}
