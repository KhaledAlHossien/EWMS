using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Responses.Commands.Submit
{
    public record SubmitStudyResponseCommand(int Id) : IRequest<StudyResponseDto>;
}
