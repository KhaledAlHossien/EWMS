using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Tasks.Commands.Update
{
    public record UpdateStudyTaskCommand(int Id, StudyTaskFormDto Dto) : IRequest<StageTaskDto>;
}
