using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Responses.Commands.Cancel
{
    public record CancelStudyResponseCommand(int Id, string Reason) : IRequest<StudyResponseDto>;
}
