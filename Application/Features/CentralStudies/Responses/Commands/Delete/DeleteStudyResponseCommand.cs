using MediatR;

namespace Application.Features.CentralStudies.Responses.Commands.Delete
{
    public record DeleteStudyResponseCommand(int Id) : IRequest<Unit>;
}
