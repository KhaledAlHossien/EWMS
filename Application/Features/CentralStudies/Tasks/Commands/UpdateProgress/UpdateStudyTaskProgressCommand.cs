using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Tasks.Commands.UpdateProgress
{
    public record UpdateStudyTaskProgressCommand(int Id, StudyTaskProgressDto Dto) : IRequest<StageTaskDto>;
}
