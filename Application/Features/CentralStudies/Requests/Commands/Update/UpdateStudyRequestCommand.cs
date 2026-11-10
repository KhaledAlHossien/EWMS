using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Requests.Commands.Update
{
    public record UpdateStudyRequestCommand(int Id, StudyRequestFormDto Dto) : IRequest<StudyRequestDto>;
}
