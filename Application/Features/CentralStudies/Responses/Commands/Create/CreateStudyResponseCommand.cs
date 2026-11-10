using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Responses.Commands.Create
{
    public record CreateStudyResponseCommand(StudyResponseFormDto Dto) : IRequest<StudyResponseDto>;
}
