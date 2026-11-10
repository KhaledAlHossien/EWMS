using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Requests.Commands.Create
{
    public record CreateStudyRequestCommand(StudyRequestFormDto Dto) : IRequest<StudyRequestDto>;
}
