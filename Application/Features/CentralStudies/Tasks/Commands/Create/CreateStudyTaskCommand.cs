using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Tasks.Commands.Create
{
    public record CreateStudyTaskCommand(StudyTaskFormDto Dto) : IRequest<StageTaskDto>;
}
