using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Requests.Commands.Submit
{
    public record SubmitStudyRequestCommand(int Id) : IRequest<StudyRequestDto>;
}
