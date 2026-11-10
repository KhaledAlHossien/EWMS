using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Requests.Commands.Cancel
{
    public record CancelStudyRequestCommand(int Id, string Reason) : IRequest<StudyRequestDto>;
}
