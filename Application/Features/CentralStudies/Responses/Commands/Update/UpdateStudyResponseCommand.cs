using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Responses.Commands.Update
{
    public record UpdateStudyResponseCommand(int Id, StudyResponseFormDto Dto) : IRequest<StudyResponseDto>;
}
