using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Responses.Commands.UpdatePlanning
{
    public record UpdateStudyResponsePlanningCommand(int Id, StudyResponsePlanningFormDto Dto) : IRequest<StudyResponseDto>;
}
